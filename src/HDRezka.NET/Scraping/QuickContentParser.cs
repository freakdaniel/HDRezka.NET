using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace HdRezka.Scraping;

internal static partial class QuickContentParser
{
    public static async Task<QuickContent> ParseAsync(
        string html,
        Uri origin,
        int mediaId,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(html, cancellationToken).ConfigureAwait(false);
        Parsing.ThrowForChallengePage(document);
        var titleLink = document.QuerySelector(".b-content__bubble_title a") ??
            throw new ParseException("The quick-content response has no media link.");
        var title = Normalize(titleLink.TextContent);
        if (title.Length == 0)
        {
            throw new ParseException("The quick-content response has no media title.");
        }

        var urlValue = titleLink.GetAttribute("href");
        if (string.IsNullOrWhiteSpace(urlValue))
        {
            throw new ParseException("The quick-content response has no media URL.");
        }

        var url = new Uri(origin, urlValue);
        var textBlocks = document.QuerySelectorAll(".b-content__bubble_text");
        var description = Normalize(textBlocks.FirstOrDefault()?.TextContent ?? "");
        var ageRating = textBlocks
            .Select(block => block.QuerySelector("b")?.TextContent)
            .Select(value => value?.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var genres = textBlocks
            .SelectMany(block => block.QuerySelectorAll("a"))
            .Select(link => ParseNamedLink(link, origin))
            .ToList();
        var ratingElement = document.QuerySelector(".b-content__bubble_rating");
        var rating = new Rating(
            ParseDouble(ratingElement?.QuerySelector("b")?.TextContent),
            ParseParenthesizedCount(ratingElement?.TextContent));
        var directors = ParsePeople(document, origin, "director", "director");
        var cast = ParsePeople(document, origin, "actor", "actor");
        var externalRatings = document
            .QuerySelectorAll(".b-content__bubble_rates > span")
            .Select(ParseExternalRating)
            .ToList();
        return new QuickContent(
            mediaId,
            title,
            url,
            CatalogParser.ParseCategory(url),
            description,
            rating,
            ageRating,
            genres,
            directors,
            cast,
            externalRatings);
    }

    private static IReadOnlyList<PersonInfo> ParsePeople(
        IDocument document,
        Uri origin,
        string itemProperty,
        string job) =>
        document
            .QuerySelectorAll($"[itemprop=\"{itemProperty}\"]")
            .Select(element =>
            {
                var link = element.QuerySelector("a") ??
                    throw new ParseException("A quick-content person has no page link.");
                var url = new Uri(origin, link.GetAttribute("href"));
                return new PersonInfo(
                    Parsing.ParseRequiredInt(element.GetAttribute("data-id"), "person ID"),
                    Normalize(link.TextContent),
                    job,
                    url,
                    ImageUrl: null);
            })
            .ToList();

    private static NamedLink ParseNamedLink(IElement link, Uri origin)
    {
        var value = link.GetAttribute("href");
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ParseException("A quick-content genre has no page URL.");
        }

        return new NamedLink(Normalize(link.TextContent), new Uri(origin, value));
    }

    private static ExternalRating ParseExternalRating(IElement element)
    {
        var label = Normalize(element.TextContent).Split(':', 2)[0];
        return new ExternalRating(
            label,
            ParseDouble(element.QuerySelector("b")?.TextContent),
            ParseCount(element.QuerySelector("i")?.TextContent),
            Url: null);
    }

    private static double? ParseDouble(string? value) =>
        double.TryParse(
            value?.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var result)
                ? result
                : null;

    private static int? ParseParenthesizedCount(string? value)
    {
        var match = ParenthesizedCountRegex().Match(value ?? "");
        return match.Success ? ParseCount(match.Groups["count"].Value) : null;
    }

    private static int? ParseCount(string? value)
    {
        var digits = string.Concat((value ?? "").Where(char.IsAsciiDigit));
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static string Normalize(string value) => WhitespaceRegex().Replace(value, " ").Trim();

    [GeneratedRegex(@"\((?<count>[\d\s\u00a0]+)\)")]
    private static partial Regex ParenthesizedCountRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
