using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace HdRezka.Scraping;

internal static partial class PersonParser
{
    public static async Task<Person> ParseAsync(
        string html,
        Uri url,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(html, cancellationToken)
            .ConfigureAwait(false);
        Parsing.ThrowForChallengePage(document);
        var root = document.QuerySelector(".b-person") ??
            throw new ParseException("The response is not a person page.");
        var name = root.QuerySelector("[itemprop=\"name\"], .b-post__title .t1")
            ?.TextContent.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ParseException("The person page has no name.");
        }

        var origin = new Uri(url.GetLeftPart(UriPartial.Authority));
        var birthTime = root.QuerySelector("[itemprop=\"birthDate\"]");
        var birthDateLabel = Normalize(birthTime?.TextContent);
        var dateValue = birthTime?.GetAttribute("datetime")?.Trim();
        var birthDate = DateOnly.TryParseExact(
            dateValue,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedDate)
                ? parsedDate
                : (DateOnly?)null;
        var birthYear = birthDate?.Year ??
            (int.TryParse(YearRegex().Match(dateValue ?? birthDateLabel ?? "").Value, out var year)
                ? year
                : (int?)null);
        var ageText = birthTime?.ParentElement?.TextContent ?? "";
        var age = int.TryParse(
            AgeRegex().Match(ageText).Groups["age"].Value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedAge)
                ? parsedAge
                : (int?)null;
        var birthplace = FindInfoValue(root, "Место рождения");
        var careers = root
            .QuerySelectorAll(".b-person__career")
            .Select(element => new PersonCareer(
                Normalize(element.QuerySelector("h2")?.TextContent) ??
                    throw new ParseException("A person career has no name."),
                Normalize(element.QuerySelector(".b-person__career_stats")?.TextContent) ?? "",
                element
                    .QuerySelectorAll(".b-content__inline_item")
                    .Select(item => CatalogParser.ParseItem(item, origin))
                    .ToList()))
            .ToList();
        return new Person(
            ParseId(url),
            name,
            Normalize(root.QuerySelector("[itemprop=\"alternativeHeadline\"], .b-post__title .t2")?.TextContent),
            url,
            TryParseUri(origin, root.QuerySelector("[itemprop=\"image\"], .b-sidecover img")?.GetAttribute("src")),
            root.QuerySelectorAll("[itemprop=\"jobTitle\"]")
                .Select(element => Normalize(element.TextContent))
                .Where(value => value is not null)
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            birthDateLabel,
            birthDate,
            birthYear,
            age,
            birthplace,
            careers);
    }

    private static int ParseId(Uri url)
    {
        var segment = url.AbsolutePath.Trim('/').Split('/').LastOrDefault();
        var prefix = segment?.Split('-', 2)[0];
        return int.TryParse(prefix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id
            : throw new ParseException("Could not parse the person identifier.");
    }

    private static string? FindInfoValue(IElement root, string label)
    {
        var row = root.QuerySelectorAll("table.b-post__info tr")
            .FirstOrDefault(element => element.QuerySelector("td.l")?.TextContent
                .Contains(label, StringComparison.OrdinalIgnoreCase) == true);
        return Normalize(row?.QuerySelectorAll("td").Skip(1).FirstOrDefault()?.TextContent);
    }

    private static Uri? TryParseUri(Uri origin, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new Uri(origin, value);

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return WhitespaceRegex().Replace(value, " ").Trim();
    }

    [GeneratedRegex(@"\b(?:18|19|20)\d{2}\b")]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"\((?<age>\d{1,3})\s+(?:год|года|лет)\)", RegexOptions.IgnoreCase)]
    private static partial Regex AgeRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
