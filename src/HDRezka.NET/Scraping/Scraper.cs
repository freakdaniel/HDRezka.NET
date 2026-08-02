using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using HdRezka.Abstractions;

namespace HdRezka.Scraping;

internal sealed partial class Scraper : IScraper
{
    public async Task<PageSnapshot> ParseMediaPageAsync(
        string html,
        Uri url,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(html, cancellationToken).ConfigureAwait(false);
        Parsing.ThrowForChallengePage(document);
        var origin = new Uri(url.GetLeftPart(UriPartial.Authority));
        var names = ParseNames(document, url);
        var originalNames = ParseOriginalNames(document);
        var translationOptions = ParseTranslators(document);
        var playback = ParsePlayback(document, translationOptions);
        var translators = translationOptions
            .GroupBy(translator => translator.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var format = ParseFormat(document);

        return new PageSnapshot(
            ParseId(document, url),
            names[0],
            names,
            originalNames.LastOrDefault(),
            originalNames,
            ParseDescription(document),
            ParseUri(
                origin,
                ParseMetaContent(document, "og:image") ??
                document.QuerySelector(".b-sidecover img")?.GetAttribute("src"),
                "thumbnail"),
            TryParseUri(origin, document.QuerySelector(".b-sidecover a")?.GetAttribute("href")),
            ParseReleaseYear(document),
            format,
            ParseCategory(url),
            ParseDetails(document, origin),
            ParseRating(document),
            AccountTokenParser.Parse(document),
            playback,
            translationOptions,
            translators,
            translationOptions
                .GroupBy(translator => translator.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.OrdinalIgnoreCase),
            ParseOtherParts(document, origin, url),
            ParseBookmarkFolderIds(document),
            document.QuerySelector("#ctrl_favs, [name=\"ctrl_favs\"]")
                ?.GetAttribute("value") ?? "",
            format == MediaFormat.Series
                ? ParseInitialSeriesInfo(document, translators)
                : null);
    }

    public EpisodeSnapshot ParseEpisodes(string seasonsHtml, string episodesHtml) =>
        Parsing.ParseEpisodes(seasonsHtml, episodesHtml);

    public MediaStream ParseStream(
        StreamSnapshot snapshot,
        int? season,
        int? episode,
        string name,
        int translatorId) =>
        Parsing.ParseStream(
            snapshot,
            season,
            episode,
            name,
            translatorId);

    public async Task<IReadOnlyList<FastSearchResult>> ParseFastSearchAsync(
        string html,
        Uri origin,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(html, cancellationToken).ConfigureAwait(false);
        return document
            .QuerySelectorAll(".b-search__section_list li")
            .Select(item =>
            {
                var title = item.QuerySelector("span.enty")?.TextContent.Trim() ??
                    throw new ParseException("A search result has no title.");
                var url = ParseUri(
                    origin,
                    item.QuerySelector("a")?.GetAttribute("href"),
                    "search result");
                var ratingText = item.QuerySelector("span.rating")?.TextContent.Trim();
                var rating = double.TryParse(
                    ratingText,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed)
                    ? parsed
                    : (double?)null;
                return new FastSearchResult(title, url, rating);
            })
            .ToList();
    }

    public async Task<PageResult<SearchResult>> ParseSearchPageAsync(
        string html,
        Uri origin,
        int page,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(html, cancellationToken).ConfigureAwait(false);
        Parsing.ThrowForChallengePage(document);
        return new PageResult<SearchResult>(
            document
                .QuerySelectorAll(".b-content__inline_item")
                .Select(item => ParseFullSearchResult(item, origin))
                .ToList(),
            page,
            CatalogParser.ParseTotalPages(document));
    }

    private static int ParseId(IDocument document, Uri url)
    {
        var candidates = new[]
        {
            document.QuerySelector("#post_id")?.GetAttribute("value"),
            document.QuerySelector("#send-video-issue")?.GetAttribute("data-id"),
            document.QuerySelector("#user-favorites-holder")?.GetAttribute("data-post_id"),
            url.Segments.LastOrDefault()?.Split('-', 2)[0]
        };
        return candidates
            .Select(value => int.TryParse(value, out var id) ? id : (int?)null)
            .FirstOrDefault(id => id.HasValue)
            ?? throw new ParseException("Could not determine the media ID.");
    }

    private static IReadOnlyList<long> ParseBookmarkFolderIds(IDocument document) =>
        document
            .QuerySelectorAll("#user-favorites-holder .user-fav-check[checked]")
            .Select(element =>
                long.TryParse(
                    element.GetAttribute("value"),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var id) &&
                id > 0
                    ? id
                    : throw new ParseException(
                        "A selected bookmark folder has an invalid identifier."))
            .ToList();

    private static IReadOnlyList<string> ParseNames(IDocument document, Uri url)
    {
        var value = document.QuerySelector(".b-post__title")?.TextContent;
        if (string.IsNullOrWhiteSpace(value))
        {
            value = ParseMetaContent(document, "og:title");
            if (!string.IsNullOrWhiteSpace(value))
            {
                value = TitleYearSuffixRegex().Replace(value, "");
            }
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ParseException(
                $"Could not find the media title at \"{url}\". Page title: \"{document.Title}\".");
        }

        return ParseTitleVariants(value);
    }

    private static IReadOnlyList<string> ParseOriginalNames(IDocument document) =>
        ParseTitleVariants(document.QuerySelector(".b-post__origtitle")?.TextContent);

    private static IReadOnlyList<string> ParseTitleVariants(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : TitleSeparatorRegex()
                .Split(value.Trim())
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .Select(title => title.Trim())
                .ToList();

    private static int? ParseReleaseYear(IDocument document)
    {
        var href = document.QuerySelector(".b-content__main .b-post__info a[href*=\"/year/\"]")
            ?.GetAttribute("href");
        var match = YearRegex().Match(
            href ??
            ParseMetaContent(document, "og:title") ??
            "");
        return match.Success ? int.Parse(match.Value, CultureInfo.InvariantCulture) : null;
    }

    private static MediaFormat ParseFormat(IDocument document) =>
        document.QuerySelector("meta[property=\"og:type\"]")?.GetAttribute("content") switch
        {
            "video.tv_series" => MediaFormat.Series,
            "video.movie" => MediaFormat.Movie,
            _ => MediaFormat.Unknown
        };

    private static MediaCategory ParseCategory(Uri url) =>
        url.AbsolutePath.TrimStart('/').Split('/', 2)[0] switch
        {
            "films" => MediaCategory.Film,
            "series" => MediaCategory.Series,
            "cartoons" => MediaCategory.Cartoon,
            "animation" => MediaCategory.Anime,
            "show" => MediaCategory.Show,
            _ => MediaCategory.Unknown
        };

    private static Rating ParseRating(IDocument document)
    {
        var wrapper = document.QuerySelector(".b-post__rating");
        if (wrapper is null)
        {
            return new Rating(null, null);
        }

        var valueText = wrapper.QuerySelector(".num")?.TextContent.Trim();
        var votesText = wrapper.QuerySelector(".votes")?.TextContent;
        var value = double.TryParse(
            valueText,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsedValue)
            ? parsedValue
            : (double?)null;
        var digits = NonDigitRegex().Replace(votesText ?? "", "");
        var votes = int.TryParse(digits, CultureInfo.InvariantCulture, out var parsedVotes)
            ? parsedVotes
            : (int?)null;
        return new Rating(value, votes);
    }

    private static MediaDetails ParseDetails(IDocument document, Uri origin)
    {
        var durationMinutes = Parsing.TryParseOptionalInt(
            NonDigitRegex().Replace(GetInfoValue(document, "Время") ?? "", ""));
        return new MediaDetails(
            EmptyToNull(GetInfoValue(document, "Слоган")),
            TryParseDate(GetInfoValue(document, "Дата выхода")),
            ParseNamedLinks(FindInfoRow(document, "Страна"), origin),
            document
                .QuerySelectorAll(".b-post__info [itemprop=\"genre\"]")
                .Select(element => ParseNamedLink(element.ParentElement, origin))
                .Where(link => link is not null)
                .Cast<NamedLink>()
                .ToList(),
            ParsePersons(document, "director", origin),
            ParsePersons(document, "actor", origin),
            EmptyToNull(GetInfoValue(document, "В качестве")),
            EmptyToNull(GetInfoValue(document, "Возраст")),
            durationMinutes.HasValue
                ? TimeSpan.FromMinutes(durationMinutes.Value)
                : null,
            ParseNamedLinks(FindInfoRow(document, "Из серии"), origin),
            ParseNamedLinks(FindInfoRow(document, "Входит в списки"), origin),
            ParseExternalRatings(document, origin),
            CatalogParser.ParseItems(document, origin),
            ParseSchedule(document));
    }

    private static IReadOnlyList<PersonInfo> ParsePersons(
        IDocument document,
        string property,
        Uri origin) =>
        document
            .QuerySelectorAll($".person-name-item[itemprop=\"{property}\"]")
            .Select(element =>
            {
                var link = element.QuerySelector("a");
                var name = element.QuerySelector("[itemprop=\"name\"]")?.TextContent.Trim();
                if (!int.TryParse(
                        element.GetAttribute("data-id"),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var id) ||
                    string.IsNullOrWhiteSpace(name) ||
                    link is null)
                {
                    return null;
                }

                var url = TryParseUri(origin, link.GetAttribute("href"));
                if (url is null)
                {
                    return null;
                }

                return new PersonInfo(
                    id,
                    name,
                    element.GetAttribute("data-job")?.Trim() ?? property,
                    url,
                    TryParseUri(origin, element.GetAttribute("data-photo")));
            })
            .Where(person => person is not null)
            .Cast<PersonInfo>()
            .ToList();

    private static IReadOnlyList<ExternalRating> ParseExternalRatings(
        IDocument document,
        Uri origin) =>
        document
            .QuerySelectorAll(".b-post__info_rates")
            .Select(element =>
            {
                var source = element.QuerySelector("a")?.TextContent.Trim();
                if (string.IsNullOrWhiteSpace(source))
                {
                    return null;
                }

                var value = double.TryParse(
                    element.QuerySelector(".bold")?.TextContent.Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsedValue)
                        ? parsedValue
                        : (double?)null;
                var voteDigits = NonDigitRegex().Replace(
                    element.QuerySelector("i")?.TextContent ?? "",
                    "");
                var votes = int.TryParse(
                    voteDigits,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsedVotes)
                        ? parsedVotes
                        : (int?)null;
                return new ExternalRating(
                    source,
                    value,
                    votes,
                    TryParseUri(
                        origin,
                        element.QuerySelector("a")?.GetAttribute("href")));
            })
            .Where(rating => rating is not null)
            .Cast<ExternalRating>()
            .ToList();

    private static IReadOnlyList<EpisodeScheduleEntry> ParseSchedule(
        IDocument document) =>
        document
            .QuerySelectorAll(".b-post__schedule_table tr")
            .Select(row =>
            {
                var identifier = row.QuerySelector(".td-1");
                var match = ScheduleEpisodeRegex().Match(
                    identifier?.TextContent ?? "");
                if (!long.TryParse(
                        identifier?.GetAttribute("data-id"),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var id) ||
                    !match.Success)
                {
                    return null;
                }

                var action = row.QuerySelector(".watch-episode-action");
                return new EpisodeScheduleEntry(
                    id,
                    int.Parse(
                        match.Groups["season"].Value,
                        CultureInfo.InvariantCulture),
                    int.Parse(
                        match.Groups["episode"].Value,
                        CultureInfo.InvariantCulture),
                    EmptyToNull(row.QuerySelector(".td-2 b")?.TextContent.Trim()),
                    EmptyToNull(row.QuerySelector(".td-2 span")?.TextContent.Trim()),
                    TryParseDate(row.QuerySelector(".td-4")?.TextContent),
                    row.QuerySelector(".exists-episode") is not null,
                    action?.ClassList.Contains("watched") == true);
            })
            .Where(entry => entry is not null)
            .Cast<EpisodeScheduleEntry>()
            .ToList();

    private static IReadOnlyList<NamedLink> ParseNamedLinks(
        IElement? container,
        Uri origin) =>
        container?
            .QuerySelectorAll("a")
            .Select(element => ParseNamedLink(element, origin))
            .Where(link => link is not null)
            .Cast<NamedLink>()
            .ToList()
        ?? [];

    private static NamedLink? ParseNamedLink(IElement? element, Uri origin)
    {
        var name = element?.TextContent.Trim();
        var url = TryParseUri(origin, element?.GetAttribute("href"));
        return string.IsNullOrWhiteSpace(name) || url is null
            ? null
            : new NamedLink(name, url);
    }

    private static IElement? FindInfoRow(IDocument document, string heading) =>
        document
            .QuerySelectorAll(".b-post__info tr")
            .FirstOrDefault(
                row => row.QuerySelector("h2")?.TextContent.Trim()
                    .Equals(heading, StringComparison.OrdinalIgnoreCase) == true);

    private static string? GetInfoValue(IDocument document, string heading)
    {
        var cells = FindInfoRow(document, heading)?.QuerySelectorAll("td");
        return cells is null || cells.Length == 0
            ? null
            : cells[^1].TextContent.Trim();
    }

    private static DateOnly? TryParseDate(string? value)
    {
        var normalized = value?
            .Replace("года", "", StringComparison.OrdinalIgnoreCase)
            .Replace('\u00A0', ' ')
            .Trim();
        return DateOnly.TryParse(
            normalized,
            CultureInfo.GetCultureInfo("ru-RU"),
            DateTimeStyles.AllowWhiteSpaces,
            out var result)
                ? result
                : null;
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<Translator> ParseTranslators(
        IDocument document)
    {
        var result = new List<Translator>();
        var list = document.QuerySelector("#translators-list");
        if (list is not null)
        {
            foreach (var element in list.Children)
            {
                if (!int.TryParse(element.GetAttribute("data-translator_id"), out var translatorId))
                {
                    continue;
                }

                var translatorName = element.TextContent.Trim();
                var language = element.QuerySelector("img")?.GetAttribute("title");
                if (!string.IsNullOrWhiteSpace(language) &&
                    !translatorName.Contains(language, StringComparison.OrdinalIgnoreCase))
                {
                    translatorName += $" ({language})";
                }

                result.Add(new Translator(
                    translatorId,
                    translatorName,
                    element.ClassList.Contains("b-prem_translator"))
                {
                    IsCamrip = ParseFlag(element.GetAttribute("data-camrip")),
                    HasAds = ParseFlag(element.GetAttribute("data-ads")),
                    IsDirectorCut = ParseFlag(element.GetAttribute("data-director"))
                });
            }
        }

        if (result.Count > 0)
        {
            return result;
        }

        var scripts = string.Join("\n", document.Scripts.Select(script => script.TextContent));
        var idMatch = TranslatorIdRegex().Match(scripts);
        if (idMatch.Success)
        {
            var fallbackName = document.QuerySelectorAll(".b-post__info tr")
                .FirstOrDefault(row => row.TextContent.Contains("переводе", StringComparison.OrdinalIgnoreCase))
                ?.QuerySelectorAll("td")
                .LastOrDefault()
                ?.TextContent.Trim() ?? "Unknown";
            var fallbackId = int.Parse(idMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            result.Add(new Translator(fallbackId, fallbackName, false));
        }

        return result;
    }

    private static PlaybackState ParsePlayback(
        IDocument document,
        IReadOnlyList<Translator> translators)
    {
        var status = document.QuerySelector(".b-player .b-post__status_wrapper") ??
            document.QuerySelector(".b-post__status_wrapper");
        if (status is not null)
        {
            var message = status.QuerySelector(".b-post__go_status") ?? status;
            var reason = WhitespaceRegex().Replace(
                string.Join(" ", message.ChildNodes.Select(node => node.TextContent)),
                " ").Trim();
            var availability = reason.Contains("восстанов", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("временно", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("работаем", StringComparison.OrdinalIgnoreCase)
                    ? PlaybackAvailability.TemporarilyUnavailable
                    : PlaybackAvailability.Unavailable;
            return new PlaybackState(
                availability,
                string.IsNullOrWhiteSpace(reason) ? null : reason);
        }

        if (translators.Count > 0)
        {
            return new PlaybackState(PlaybackAvailability.Available, null);
        }

        if (document.QuerySelector(".b-player, #player") is not null)
        {
            return new PlaybackState(PlaybackAvailability.Unavailable, null);
        }

        throw new ParseException("Could not determine any translators or a player status.");
    }

    private static string ParseDescription(IDocument document) =>
        document.QuerySelector("[itemprop=\"description\"]")?.GetAttribute("content")?.Trim() ??
        document.QuerySelector(".b-post__description_text")?.TextContent.Trim() ??
        ParseMetaContent(document, "og:description") ??
        document.QuerySelector("meta[name=\"description\"]")?.GetAttribute("content")?.Trim() ??
        "";

    private static string? ParseMetaContent(IDocument document, string property) =>
        document.QuerySelector($"meta[property=\"{property}\"]")
            ?.GetAttribute("content")
            ?.Trim();

    private static SeriesInfo? ParseInitialSeriesInfo(
        IDocument document,
        IReadOnlyDictionary<int, Translator> translators)
    {
        var seasonElements = document.QuerySelectorAll(".b-simple_season__item");
        var episodeElements = document.QuerySelectorAll(".b-simple_episode__item");
        if (seasonElements.Length == 0 || episodeElements.Length == 0)
        {
            return null;
        }

        var activeTranslatorId = Parsing.TryParseOptionalInt(
            document.QuerySelector("#translators-list [data-translator_id].active")
                ?.GetAttribute("data-translator_id"));
        var translator = activeTranslatorId.HasValue
            ? translators.GetValueOrDefault(activeTranslatorId.Value)
            : translators.Count == 1
                ? translators.Values.Single()
                : null;
        if (translator is null)
        {
            return null;
        }

        var seasons = seasonElements.ToDictionary(
            element => Parsing.ParseRequiredInt(
                element.GetAttribute("data-tab_id"),
                "season ID"),
            element => element.TextContent.Trim());
        var episodes = new Dictionary<int, Dictionary<int, string>>();
        foreach (var element in episodeElements)
        {
            var season = Parsing.ParseRequiredInt(
                element.GetAttribute("data-season_id"),
                "season ID");
            var episode = Parsing.ParseRequiredInt(
                element.GetAttribute("data-episode_id"),
                "episode ID");
            if (!episodes.TryGetValue(season, out var seasonEpisodes))
            {
                seasonEpisodes = [];
                episodes[season] = seasonEpisodes;
            }

            seasonEpisodes[episode] = element.TextContent.Trim();
        }

        return new SeriesInfo(
            translator.Id,
            translator.Name,
            translator.IsPremium,
            seasons,
            episodes.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyDictionary<int, string>)pair.Value));
    }

    private static bool ParseFlag(string? value) =>
        value is "1" or "true";

    private static IReadOnlyList<RelatedPart> ParseOtherParts(
        IDocument document,
        Uri origin,
        Uri currentUrl) =>
        document
            .QuerySelectorAll(".b-post__partcontent_item")
            .Select(item =>
            {
                var title = item.QuerySelector(".title")?.TextContent.Trim() ?? "";
                var url = item.ClassList.Contains("current")
                    ? currentUrl
                    : TryParseUri(origin, item.GetAttribute("data-url"));
                return url is null ? null : new RelatedPart(title, url);
            })
            .Where(item => item is not null)
            .Cast<RelatedPart>()
            .ToList();

    private static SearchResult ParseFullSearchResult(IElement item, Uri origin)
    {
        var link = item.QuerySelector(".b-content__inline_item-link a");
        var cover = item.QuerySelector(".b-content__inline_item-cover img");
        var title = link?.TextContent.Trim() ??
            throw new ParseException("A search result has no title.");
        var url = ParseUri(origin, link.GetAttribute("href"), "search result");
        var image = ParseUri(origin, cover?.GetAttribute("src"), "search cover");
        var categoryElement = item.QuerySelector(".cat");
        var category = categoryElement is null
            ? MediaCategory.Unknown
            : DetectCategory(categoryElement.ClassList);
        return new SearchResult(title, url, image, category);
    }

    private static MediaCategory DetectCategory(IEnumerable<string> classes)
    {
        var set = classes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (set.Contains("films")) return MediaCategory.Film;
        if (set.Contains("series")) return MediaCategory.Series;
        if (set.Contains("cartoons")) return MediaCategory.Cartoon;
        if (set.Contains("animation")) return MediaCategory.Anime;
        if (set.Contains("show")) return MediaCategory.Show;
        return MediaCategory.Unknown;
    }

    private static Uri ParseUri(Uri origin, string? value, string description) =>
        TryParseUri(origin, value) ??
        throw new ParseException($"Could not parse the {description} URL.");

    private static Uri? TryParseUri(Uri origin, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new Uri(origin, value);

    [GeneratedRegex(@"\d{4}")]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"\s+\(\d{4}\)\s*$")]
    private static partial Regex TitleYearSuffixRegex();

    [GeneratedRegex(@"\s+/\s+")]
    private static partial Regex TitleSeparatorRegex();

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigitRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"(?<season>\d+)\s+сезон\s+(?<episode>\d+)\s+серия", RegexOptions.IgnoreCase)]
    private static partial Regex ScheduleEpisodeRegex();

    [GeneratedRegex(@"initCDN(?:Series|Movies)Events\s*\([^,]+,\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex TranslatorIdRegex();
}
