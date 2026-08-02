using System.Globalization;
using System.Text.RegularExpressions;

namespace HdRezka.Scraping;

internal static partial class FranchiseParser
{
    public static async Task<PageResult<FranchiseSummary>> ParseDirectoryAsync(
        string html,
        Uri origin,
        int page,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(html, cancellationToken)
            .ConfigureAwait(false);
        Parsing.ThrowForChallengePage(document);
        var items = document.QuerySelectorAll(".b-content__collections_item")
            .Select(item =>
            {
                var link = item.QuerySelector("a.title") ??
                    throw new ParseException("A franchise has no page link.");
                var url = ParseUri(origin, link.GetAttribute("href"), "franchise");
                var match = FranchiseIdRegex().Match(url.AbsolutePath);
                var title = link.TextContent.Trim();
                if (!match.Success || string.IsNullOrWhiteSpace(title))
                {
                    throw new ParseException("A franchise has no valid identifier or title.");
                }

                return new FranchiseSummary(
                    int.Parse(match.Groups["id"].Value, CultureInfo.InvariantCulture),
                    title,
                    url,
                    ParseUri(origin, item.QuerySelector("img.cover")?.GetAttribute("src"), "franchise cover"),
                    ParseRequiredInt(item.QuerySelector(".num")?.TextContent, "franchise part count"));
            })
            .ToList();
        return new PageResult<FranchiseSummary>(
            items,
            page,
            CatalogParser.ParseTotalPages(document));
    }

    public static async Task<Franchise> ParseAsync(
        string html,
        Uri url,
        int id,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(html, cancellationToken)
            .ConfigureAwait(false);
        Parsing.ThrowForChallengePage(document);
        var title = document.QuerySelector("h1")?.TextContent.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ParseException("The franchise page has no heading.");
        }

        var origin = new Uri(url.GetLeftPart(UriPartial.Authority));
        var parts = document.QuerySelectorAll(".b-post__partcontent_item")
            .Select(item =>
            {
                var link = item.QuerySelector(".title a") ??
                    throw new ParseException("A franchise part has no media link.");
                var mediaUrl = ParseUri(origin, link.GetAttribute("href"), "franchise part");
                var partTitle = link.TextContent.Trim();
                if (string.IsNullOrWhiteSpace(partTitle))
                {
                    throw new ParseException("A franchise part has no title.");
                }

                return new FranchisePart(
                    ParseRequiredInt(item.QuerySelector(".num")?.TextContent, "franchise part order"),
                    ParseMediaId(mediaUrl),
                    partTitle,
                    mediaUrl,
                    TryParseInt(item.QuerySelector(".year")?.TextContent),
                    double.TryParse(
                        item.QuerySelector(".rating")?.TextContent.Trim(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var rating)
                            ? rating
                            : null);
            })
            .OrderBy(part => part.Order)
            .ToList();
        var imageValue = document
            .QuerySelector("meta[property=\"og:image\"]")
            ?.GetAttribute("content");
        return new Franchise(
            id,
            title,
            url,
            string.IsNullOrWhiteSpace(imageValue) ? null : new Uri(origin, imageValue),
            parts);
    }

    private static int? ParseMediaId(Uri url)
    {
        var segment = url.Segments.LastOrDefault()?.Trim('/');
        return int.TryParse(segment?.Split('-', 2)[0], out var id) ? id : null;
    }

    private static int ParseRequiredInt(string? value, string description) =>
        TryParseInt(value) ?? throw new ParseException($"Could not parse the {description}.");

    private static int? TryParseInt(string? value)
    {
        var match = IntegerRegex().Match(value ?? "");
        return int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static Uri ParseUri(Uri origin, string? value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ParseException($"Could not parse the {description} URL.");
        }

        return new Uri(origin, value);
    }

    [GeneratedRegex(@"/franchises/(?<id>\d+)-", RegexOptions.IgnoreCase)]
    private static partial Regex FranchiseIdRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex IntegerRegex();
}
