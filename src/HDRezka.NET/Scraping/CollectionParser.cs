using System.Globalization;
using System.Text.RegularExpressions;

namespace HdRezka.Scraping;

internal static partial class CollectionParser
{
    public static async Task<PageResult<CollectionSummary>> ParseDirectoryAsync(
        string html,
        Uri origin,
        int page,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(html, cancellationToken).ConfigureAwait(false);
        Parsing.ThrowForChallengePage(document);
        var items = document
            .QuerySelectorAll(".b-content__collections_item")
            .Select(item =>
            {
                var link = item.QuerySelector("a.title") ??
                    throw new ParseException("A collection has no page link.");
                var url = ParseUri(origin, link.GetAttribute("href"), "collection");
                var idMatch = CollectionIdRegex().Match(url.AbsolutePath);
                if (!idMatch.Success)
                {
                    throw new ParseException("A collection URL has no numeric identifier.");
                }

                var title = link.TextContent.Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    throw new ParseException("A collection has no title.");
                }

                var imageUrl = ParseUri(
                    origin,
                    item.QuerySelector("img.cover")?.GetAttribute("src"),
                    "collection cover");
                var count = int.TryParse(
                    item.QuerySelector(".num")?.TextContent.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsedCount)
                        ? parsedCount
                        : throw new ParseException("A collection has no valid item count.");
                return new CollectionSummary(
                    int.Parse(
                        idMatch.Groups["id"].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture),
                    title,
                    url,
                    imageUrl,
                    count);
            })
            .ToList();
        return new PageResult<CollectionSummary>(
            items,
            page,
            CatalogParser.ParseTotalPages(document));
    }

    public static async Task<CollectionPage> ParseCollectionAsync(
        string html,
        Uri url,
        int collectionId,
        int page,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(html, cancellationToken).ConfigureAwait(false);
        Parsing.ThrowForChallengePage(document);
        var title = document.QuerySelector("h1")?.TextContent.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ParseException("The collection page has no heading.");
        }

        var description = document
            .QuerySelector("meta[property=\"og:description\"], meta[name=\"description\"]")
            ?.GetAttribute("content")
            ?.Trim();
        return new CollectionPage(
            collectionId,
            title,
            url,
            string.IsNullOrWhiteSpace(description) ? null : description,
            CatalogParser.ParseItems(document, new Uri(url.GetLeftPart(UriPartial.Authority))),
            page,
            CatalogParser.ParseTotalPages(document));
    }

    private static Uri ParseUri(Uri origin, string? value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ParseException($"Could not parse the {description} URL.");
        }

        return new Uri(origin, value);
    }

    [GeneratedRegex(@"/collections/(?<id>\d+)-", RegexOptions.IgnoreCase)]
    private static partial Regex CollectionIdRegex();
}
