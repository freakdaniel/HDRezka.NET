using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace HdRezka.Scraping;

internal static partial class AccountParser
{
    public static async Task<AccountProfile> ParseProfileAsync(
        string html,
        Uri origin,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(html, cancellationToken).ConfigureAwait(false);
        Parsing.ThrowForChallengePage(document);
        var id = ParseRequiredInt(
            document.QuerySelector("#member_user_id")?.GetAttribute("value"),
            "account identifier");
        var username = document.Title?.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ParseException("The profile page has no username.");
        }

        var email = document.QuerySelector("#email")?.GetAttribute("value")?.Trim();
        var avatar = TryParseUri(
            origin,
            document.QuerySelector("#avatar-profile img")?.GetAttribute("src"));
        var continueCount = int.TryParse(
            document.QuerySelector("#saves-count")?.TextContent.Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedCount)
                ? parsedCount
                : 0;
        return new AccountProfile(
            id,
            username,
            string.IsNullOrWhiteSpace(email) ? null : email,
            avatar,
            AccountTokenParser.Parse(document),
            continueCount,
            new Uri(origin, $"/user/{id}/"))
        {
            Gender = ParseGender(document.QuerySelector("select[name=\"gender\"]")?.GetAttribute("value") ??
                document.QuerySelector("select[name=\"gender\"] option[selected]")?.GetAttribute("value"))
        };
    }

    public static async Task<PlaybackPreferences> ParsePreferencesAsync(
        string html,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(html, cancellationToken)
            .ConfigureAwait(false);
        Parsing.ThrowForChallengePage(document);
        var form = document.QuerySelector("form#userinfo") ??
            throw new ParseException("The personality settings page has no update form.");
        return new PlaybackPreferences(
            IsChecked(form, "ctrl_links"),
            IsChecked(form, "cdn_autoswitch"),
            IsChecked(form, "cdn_first_episode"));
    }

    public static async Task<IReadOnlyList<ContinueWatchingEntry>> ParseContinueWatchingAsync(
        string html,
        Uri origin,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(html, cancellationToken).ConfigureAwait(false);
        Parsing.ThrowForChallengePage(document);
        return document
            .QuerySelectorAll(".b-videosaves__list_item[id^=\"videosave-\"]")
            .Select(item => ParseContinueWatchingItem(item, origin))
            .ToList();
    }

    public static async Task<BookmarkPageSnapshot> ParseBookmarksAsync(
        string html,
        Uri origin,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(html, cancellationToken).ConfigureAwait(false);
        Parsing.ThrowForChallengePage(document);
        var folders = document
            .QuerySelectorAll(".b-favorites_content__cats_list_item")
            .Select(item =>
            {
                var id = ParseRequiredLong(
                    item.GetAttribute("data-cat_id"),
                    "bookmark folder identifier");
                var link = item.QuerySelector("a.b-favorites_content__cats_list_link") ??
                    throw new ParseException("A bookmark folder has no page link.");
                var name = link.QuerySelector(".name")?.TextContent.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ParseException("A bookmark folder has no name.");
                }

                var count = int.TryParse(
                    link.QuerySelector(".num-holder b")?.TextContent.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsedCount)
                        ? parsedCount
                        : 0;
                return new BookmarkFolderReference(
                    id,
                    name,
                    count,
                    ParseRequiredUri(origin, link.GetAttribute("href"), "bookmark folder"),
                    link.ClassList.Contains("active"));
            })
            .ToList();
        return new BookmarkPageSnapshot(
            folders,
            folders.FirstOrDefault(folder => folder.IsActive)?.Id,
            CatalogParser.ParseItems(document, origin));
    }

    public static async Task<AccountFormSnapshot> ParseUpdateFormAsync(
        string html,
        Uri origin,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(html, cancellationToken)
            .ConfigureAwait(false);
        Parsing.ThrowForChallengePage(document);
        var form = document.QuerySelector("form#userinfo") ??
            throw new ParseException("The account settings page has no update form.");
        var actionValue = form.GetAttribute("action");
        if (string.IsNullOrWhiteSpace(actionValue))
        {
            throw new ParseException("The account update form has no action URL.");
        }

        var userId = ParseRequiredInt(
            form.QuerySelector("input[name=\"username_id\"]")?.GetAttribute("value"),
            "account identifier");
        var hash = form
            .QuerySelector("input[name=\"dle_allow_hash\"]")
            ?.GetAttribute("value")
            ?.Trim();
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new ParseException("The account update form has no security token.");
        }

        return new AccountFormSnapshot(
            new Uri(origin, actionValue),
            userId,
            hash,
            form.QuerySelector("input[name=\"email\"]")?.GetAttribute("value") ?? "",
            form.QuerySelector("select[name=\"gender\"] option[selected]")
                ?.GetAttribute("value") ?? "");
    }

    public static async Task<AccountUpdateResult> ParseUpdateResponseAsync(
        string html,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(html, cancellationToken)
            .ConfigureAwait(false);
        Parsing.ThrowForChallengePage(document);
        var errors = document
            .QuerySelectorAll(".b-list-errors li")
            .Select(item => NormalizeText(item.TextContent))
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToList();
        if (errors.Count > 0)
        {
            throw new AccountUpdateException(string.Join(" ", errors));
        }

        var message = NormalizeText(
            document.QuerySelector(".b-info__message")?.TextContent ?? "");
        return new AccountUpdateResult(message);
    }

    private static ContinueWatchingEntry ParseContinueWatchingItem(
        IElement item,
        Uri origin)
    {
        var elementId = item.Id;
        var idValue = elementId?.StartsWith("videosave-", StringComparison.Ordinal) == true
            ? elementId["videosave-".Length..]
            : item.QuerySelector(".controls [data-id]")?.GetAttribute("data-id");
        var id = ParseRequiredLong(idValue, "saved position identifier");
        var titleLink = item.QuerySelector(".title > a") ??
            throw new ParseException("A saved viewing position has no media link.");
        var title = titleLink.TextContent.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ParseException("A saved viewing position has no title.");
        }

        var url = ParseRequiredUri(origin, titleLink.GetAttribute("href"), "saved media");
        var imageUrl = ParseRequiredUri(
            origin,
            titleLink.GetAttribute("data-cover_url"),
            "saved media cover");
        var dateLabel = item.QuerySelector(".date")?.TextContent.Trim() ?? "";
        var date = DateOnly.TryParseExact(
            dateLabel,
            "dd-MM-yyyy",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedDate)
                ? parsedDate
                : (DateOnly?)null;
        var details = item.QuerySelector(".title small")?.TextContent.Trim() ?? "";
        var informationElement = item.QuerySelector(".info");
        var playbackInformation = informationElement is null
            ? ""
            : string.Join(
                " ",
                informationElement.ChildNodes
                    .Where(node => node.NodeType == NodeType.Text)
                    .Select(node => node.TextContent.Trim())
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
        var playbackMatch = PlaybackRegex().Match(playbackInformation);
        var season = TryParseGroup(playbackMatch, "season");
        var episode = TryParseGroup(playbackMatch, "episode");
        var translator = playbackMatch.Success
            ? playbackMatch.Groups["translator"].Value.Trim()
            : playbackInformation.Trim();
        var remainingEpisodes = int.TryParse(
            item.QuerySelector(".new-episode b")?.TextContent.Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedRemaining)
                ? parsedRemaining
                : (int?)null;
        return new ContinueWatchingEntry(
            id,
            title,
            url,
            imageUrl,
            CatalogParser.ParseCategory(url),
            dateLabel,
            date,
            details,
            playbackInformation,
            season,
            episode,
            string.IsNullOrWhiteSpace(translator) ? null : translator,
            item.ClassList.Contains("watched-row"),
            remainingEpisodes);
    }

    private static int? TryParseGroup(Match match, string name) =>
        match.Success &&
        int.TryParse(
            match.Groups[name].Value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;

    private static int ParseRequiredInt(string? value, string description) =>
        int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var result)
                ? result
                : throw new ParseException($"Could not parse the {description}.");

    private static AccountGender ParseGender(string? value) => value switch
    {
        "1" => AccountGender.Male,
        "2" => AccountGender.Female,
        _ => AccountGender.Unspecified
    };

    private static bool IsChecked(IElement form, string name) =>
        form.QuerySelector($"input[name=\"{name}\"][checked]") is not null;

    private static long ParseRequiredLong(string? value, string description) =>
        long.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var result)
            ? result
            : throw new ParseException($"Could not parse the {description}.");

    private static Uri ParseRequiredUri(Uri origin, string? value, string description) =>
        TryParseUri(origin, value) ??
        throw new ParseException($"Could not parse the {description} URL.");

    private static Uri? TryParseUri(Uri origin, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new Uri(origin, value);

    private static string NormalizeText(string value) =>
        WhitespaceRegex().Replace(value, " ").Trim();

    [GeneratedRegex(
        @"(?<season>\d+)\s+сезон\s+(?<episode>\d+)\s+серия(?:\s+\((?<translator>[^)]+)\))?",
        RegexOptions.IgnoreCase)]
    private static partial Regex PlaybackRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}

internal sealed record AccountFormSnapshot(
    Uri Action,
    int UserId,
    string SecurityToken,
    string Email,
    string Gender);

internal sealed record BookmarkPageSnapshot(
    IReadOnlyList<BookmarkFolderReference> Folders,
    long? ActiveFolderId,
    IReadOnlyList<CatalogItem> Items);

internal sealed record BookmarkFolderReference(
    long Id,
    string Name,
    int ItemCount,
    Uri Url,
    bool IsActive);
