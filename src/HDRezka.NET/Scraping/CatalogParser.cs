using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace HdRezka.Scraping;

internal static partial class CatalogParser
{
    public static async Task<PageResult<CatalogItem>> ParsePageAsync(
        string html,
        Uri origin,
        int page,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(html, cancellationToken).ConfigureAwait(false);
        Parsing.ThrowForChallengePage(document);
        return new PageResult<CatalogItem>(
            ParseItems(document, origin),
            page,
            ParseTotalPages(document));
    }

    public static IReadOnlyList<CatalogItem> ParseItems(
        IDocument document,
        Uri origin) =>
        document
            .QuerySelectorAll(".b-content__inline_item")
            .Select(item => ParseItem(item, origin))
            .ToList();

    public static async Task<IReadOnlyList<CatalogItem>> ParseItemsAsync(
        string html,
        Uri origin,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(html, cancellationToken).ConfigureAwait(false);
        Parsing.ThrowForChallengePage(document);
        return ParseItems(document, origin);
    }

    public static CatalogItem ParseItem(IElement item, Uri origin)
    {
        var link = item.QuerySelector(".b-content__inline_item-link a") ??
            throw new ParseException("A catalog item has no media link.");
        var title = link.TextContent.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ParseException("A catalog item has no title.");
        }

        var url = ParseUri(origin, link.GetAttribute("href"), "catalog item");
        var imageUrl = ParseUri(
            origin,
            item.QuerySelector(".b-content__inline_item-cover img")?.GetAttribute("src"),
            "catalog cover");
        var id = int.TryParse(
            item.GetAttribute("data-id"),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedId)
                ? parsedId
                : TryParseIdFromUrl(url);
        var details = item.QuerySelector(".b-content__inline_item-link > div")
            ?.TextContent.Trim() ?? "";
        var information = item.QuerySelector(".b-content__inline_item-cover .info")
            ?.TextContent.Trim();
        var category = DetectCategory(
            item.QuerySelector(".b-content__inline_item-cover .cat")?.ClassList,
            url);
        var detailParts = details
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        IReadOnlyList<int> years = detailParts.Length == 0
            ? []
            : YearRegex()
                .Matches(detailParts[0])
                .Select(match => int.Parse(match.Value, CultureInfo.InvariantCulture))
                .Distinct()
                .ToList();
        IReadOnlyList<string> countries = detailParts.Length > 1 ? [detailParts[1]] : [];
        IReadOnlyList<string> genres = detailParts.Length > 2 ? detailParts[2..] : [];
        var rating = double.TryParse(
            item.QuerySelector(".b-category-bestrating")?.TextContent.Trim(' ', '(', ')'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsedRating)
                ? parsedRating
                : (double?)null;
        return new CatalogItem(
            id,
            title,
            url,
            imageUrl,
            category,
            details,
            string.IsNullOrWhiteSpace(information) ? null : information)
        {
            Rating = rating,
            Years = years,
            Countries = countries,
            Genres = genres,
            HasTrailer = item.QuerySelector(".show-trailer") is not null
        };
    }

    public static int ParseTotalPages(IDocument document)
    {
        var pages = document
            .QuerySelectorAll(".b-navigation a")
            .Select(link => int.TryParse(
                link.TextContent.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var page)
                    ? page
                    : (int?)null)
            .Where(page => page.HasValue)
            .Select(page => page!.Value);
        return pages.DefaultIfEmpty(1).Max();
    }

    public static MediaCategory ParseCategory(Uri url) =>
        DetectCategory(classes: null, url);

    private static MediaCategory DetectCategory(
        IEnumerable<string>? classes,
        Uri url)
    {
        var set = classes?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        if (set.Contains("films")) return MediaCategory.Film;
        if (set.Contains("series")) return MediaCategory.Series;
        if (set.Contains("cartoons")) return MediaCategory.Cartoon;
        if (set.Contains("animation")) return MediaCategory.Anime;
        if (set.Contains("show")) return MediaCategory.Show;

        return url.AbsolutePath.TrimStart('/').Split('/', 2)[0] switch
        {
            "films" => MediaCategory.Film,
            "series" => MediaCategory.Series,
            "cartoons" => MediaCategory.Cartoon,
            "animation" => MediaCategory.Anime,
            "show" => MediaCategory.Show,
            _ => MediaCategory.Unknown
        };
    }

    private static int? TryParseIdFromUrl(Uri url)
    {
        var name = url.Segments.LastOrDefault()?.Trim('/');
        var prefix = name?.Split('-', 2)[0];
        return int.TryParse(
            prefix,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var id)
                ? id
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

    [GeneratedRegex(@"\b(?:18|19|20)\d{2}\b")]
    private static partial Regex YearRegex();
}
