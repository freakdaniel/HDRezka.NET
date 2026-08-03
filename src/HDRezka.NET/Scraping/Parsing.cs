using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using HdRezka.Abstractions;
using HdRezka.Observability;

namespace HdRezka.Scraping;

internal static partial class Parsing
{
    public static async Task<IDocument> ParseDocumentAsync(
        string html,
        CancellationToken cancellationToken = default)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var parser = new HtmlParser();
            var document = await parser.ParseDocumentAsync(html, cancellationToken).ConfigureAwait(false);
            Telemetry.ParseCompleted(
                "html",
                System.Diagnostics.Stopwatch.GetElapsedTime(started),
                succeeded: true);
            return document;
        }
        catch
        {
            Telemetry.ParseCompleted(
                "html",
                System.Diagnostics.Stopwatch.GetElapsedTime(started),
                succeeded: false);
            throw;
        }
    }

    public static void ThrowForChallengePage(IDocument document)
    {
        var title = (document.Title ?? "").Trim();
        if (title.Equals("Sign In", StringComparison.OrdinalIgnoreCase) ||
            title.Equals("Вход", StringComparison.OrdinalIgnoreCase) ||
            document.QuerySelector("form[action=\"/ajax/login/\"]") is not null)
        {
            throw new LoginRequiredException();
        }

        if (title.Equals("Verify", StringComparison.OrdinalIgnoreCase))
        {
            throw new CaptchaException();
        }
    }

    public static EpisodeSnapshot ParseEpisodes(string seasonsHtml, string episodesHtml)
    {
        var parser = new HtmlParser();
        var seasonsDocument = parser.ParseDocument(seasonsHtml);
        var episodesDocument = parser.ParseDocument(episodesHtml);

        var seasons = seasonsDocument
            .QuerySelectorAll(".b-simple_season__item")
            .Select(element => (
                Id: ParseRequiredInt(element.GetAttribute("data-tab_id"), "season ID"),
                Title: element.TextContent.Trim()))
            .ToDictionary(item => item.Id, item => item.Title);

        var episodes = new Dictionary<int, Dictionary<int, string>>();
        foreach (var element in episodesDocument.QuerySelectorAll(".b-simple_episode__item"))
        {
            var season = ParseRequiredInt(element.GetAttribute("data-season_id"), "season ID");
            var episode = ParseRequiredInt(element.GetAttribute("data-episode_id"), "episode ID");
            if (!episodes.TryGetValue(season, out var seasonEpisodes))
            {
                seasonEpisodes = [];
                episodes[season] = seasonEpisodes;
            }

            seasonEpisodes[episode] = element.TextContent.Trim();
        }

        var selectedEpisodeElement = episodesDocument.QuerySelector(
            ".b-simple_episode__item.active");
        var selectedEpisode = TryParseOptionalInt(
            selectedEpisodeElement?.GetAttribute("data-episode_id"));
        var selectedSeason =
            TryParseOptionalInt(selectedEpisodeElement?.GetAttribute("data-season_id")) ??
            TryParseOptionalInt(
                seasonsDocument.QuerySelector(".b-simple_season__item.active")
                    ?.GetAttribute("data-tab_id"));

        return new EpisodeSnapshot(
            seasons,
            episodes.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyDictionary<int, string>)pair.Value),
            selectedSeason,
            selectedEpisode);
    }

    public static string DecodeStreamPayload(string data)
    {
        var encodedTrash = new HashSet<string>(StringComparer.Ordinal);
        var symbols = new[] { '@', '#', '!', '^', '$' };

        foreach (var length in new[] { 2, 3 })
        {
            BuildTrashCodes(symbols, length, [], encodedTrash);
        }

        var payload = string.Concat(data.Replace("#h", "", StringComparison.Ordinal).Split("//_//"));
        foreach (var trash in encodedTrash)
        {
            payload = payload.Replace(trash, "", StringComparison.Ordinal);
        }

        try
        {
            var padding = (4 - (payload.Length % 4)) % 4;
            return Encoding.UTF8.GetString(Convert.FromBase64String(payload + new string('=', padding)));
        }
        catch (FormatException)
        {
            return payload;
        }
    }

    public static MediaStream ParseStream(
        StreamSnapshot snapshot,
        int? season,
        int? episode,
        string name,
        int translatorId)
    {
        var subtitles = ParseSubtitles(snapshot.SubtitleData, snapshot.SubtitleLanguages);
        var stream = new MediaStream(
            season,
            episode,
            name,
            translatorId,
            subtitles,
            snapshot.DefaultQuality,
            snapshot.DefaultSubtitle,
            snapshot.ThumbnailPreview,
            snapshot.IsPremiumContent,
            snapshot.AccountTier);

        foreach (Match match in MediaEntryRegex().Matches(DecodeStreamPayload(snapshot.Payload)))
        {
            var rawQuality = match.Groups["label"].Value;
            var quality = HtmlTagRegex().Replace(rawQuality, "").Trim();
            var requiresPremium =
                rawQuality.Contains("pjs-prem-quality", StringComparison.OrdinalIgnoreCase) ||
                IsPremiumQuality(quality);
            var urls = new List<Uri>();
            foreach (var candidate in match.Groups["value"].Value.Split(
                " or ",
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (Uri.TryCreate(candidate, UriKind.Absolute, out var url) &&
                    IsSupportedVideoUrl(url))
                {
                    if (!urls.Contains(url))
                    {
                        urls.Add(url);
                    }
                }
            }

            if (urls.Count > 0)
            {
                stream.AddQuality(quality, requiresPremium, urls);
            }
        }

        if (stream.Qualities.Count == 0)
        {
            throw new ParseException("The stream response did not contain any MP4 or HLS URLs.");
        }

        return stream;
    }

    private static Subtitles ParseSubtitles(
        string? data,
        IReadOnlyDictionary<string, string>? codes)
    {
        var items = new Dictionary<string, Subtitle>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(data))
        {
            return new Subtitles(items);
        }

        foreach (Match match in MediaEntryRegex().Matches(data))
        {
            var title = match.Groups["label"].Value.Trim();
            var value = match.Groups["value"].Value.Trim();
            if (!Uri.TryCreate(value, UriKind.Absolute, out var url))
            {
                continue;
            }

            var language = codes is not null && codes.TryGetValue(title, out var code)
                ? code
                : title;
            items[language] = new Subtitle(language, title, url);
        }

        return new Subtitles(items);
    }

    public static int ParseRequiredInt(string? value, string description)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        throw new ParseException($"Could not parse {description}.");
    }

    public static int? TryParseOptionalInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static bool IsSupportedVideoUrl(Uri url)
    {
        if (url.Scheme is not ("http" or "https"))
        {
            return false;
        }

        return url.AbsolutePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
            url.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPremiumQuality(string quality)
    {
        if (quality.Equals("1080p Ultra", StringComparison.OrdinalIgnoreCase) ||
            quality.Equals("2K", StringComparison.OrdinalIgnoreCase) ||
            quality.Equals("4K", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var match = ResolutionRegex().Match(quality);
        return match.Success &&
            int.TryParse(match.Groups["height"].Value, out var height) &&
            height > 1080;
    }

    private static void BuildTrashCodes(
        char[] symbols,
        int remaining,
        List<char> current,
        ISet<string> output)
    {
        if (remaining == 0)
        {
            output.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes([.. current])));
            return;
        }

        foreach (var symbol in symbols)
        {
            current.Add(symbol);
            BuildTrashCodes(symbols, remaining - 1, current, output);
            current.RemoveAt(current.Count - 1);
        }
    }

    [GeneratedRegex(@"\[(?<label>[^\]]+)\](?<value>.*?)(?=,\[|$)", RegexOptions.Singleline)]
    private static partial Regex MediaEntryRegex();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"(?<height>\d{3,4})p", RegexOptions.IgnoreCase)]
    private static partial Regex ResolutionRegex();
}
