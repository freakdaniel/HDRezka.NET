using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace HdRezka.Scraping;

internal static partial class PaymentParser
{
    public static async Task<IReadOnlyList<PaymentHistoryEntry>> ParseHistoryAsync(
        string html,
        Uri origin,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(html, cancellationToken)
            .ConfigureAwait(false);
        Parsing.ThrowForChallengePage(document);
        return document.QuerySelectorAll(".b-payments_table tbody tr")
            .Select(row =>
            {
                var cells = row.QuerySelectorAll("td");
                if (cells.Length < 5)
                {
                    throw new ParseException("A payment history row has too few columns.");
                }

                var statusLabel = Normalize(cells[3].TextContent);
                var detailsValue = row.GetAttribute("data-url");
                return new PaymentHistoryEntry(
                    ParseRequiredInt(cells[0].TextContent, "payment row number"),
                    Normalize(cells[1].TextContent),
                    ParseRequiredInt(cells[2].TextContent, "Premium duration"),
                    ParseStatus(cells[3], statusLabel),
                    statusLabel,
                    Normalize(cells[4].TextContent),
                    string.IsNullOrWhiteSpace(detailsValue) ? null : new Uri(origin, detailsValue));
            })
            .ToList();
    }

    public static async Task<PremiumOffers> ParseOffersAsync(
        string html,
        Uri origin,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(html, cancellationToken)
            .ConfigureAwait(false);
        Parsing.ThrowForChallengePage(document);
        var methods = document.QuerySelectorAll("input.payment_method-radio")
            .Select(input =>
            {
                var id = input.GetAttribute("value")?.Trim();
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new ParseException("A Premium payment method has no identifier.");
                }

                var label = document.QuerySelector($"label[for=\"{input.Id}\"]") ??
                    throw new ParseException("A Premium payment method has no label.");
                var name = Normalize(label.QuerySelector("div")?.TextContent);
                var description = Normalize(label.TextContent);
                if (!string.IsNullOrEmpty(name) && description.StartsWith(name, StringComparison.Ordinal))
                {
                    description = description[name.Length..].Trim();
                }

                var imageValue = label.GetAttribute("data-icon_url");
                return new PremiumPaymentMethod(
                    id,
                    string.IsNullOrWhiteSpace(name) ? id : name,
                    description,
                    string.IsNullOrWhiteSpace(imageValue) ? null : new Uri(origin, imageValue));
            })
            .ToList();
        var plans = document.QuerySelectorAll(".pl-item input[type=\"radio\"]")
            .Select(input =>
            {
                var match = PlanNameRegex().Match(input.GetAttribute("name") ?? "");
                if (!match.Success)
                {
                    return null;
                }

                var days = ParseRequiredInt(input.GetAttribute("value"), "Premium plan duration");
                var label = document.QuerySelector($"label[for=\"{input.Id}\"]") ??
                    throw new ParseException("A Premium plan has no label.");
                var titleElement = label.QuerySelector(".pl-title");
                var popular = titleElement?.QuerySelector("span") is not null;
                var title = Normalize(
                    string.Join(" ", titleElement?.ChildNodes
                        .Where(node => node.NodeType == NodeType.Text)
                        .Select(node => node.TextContent) ?? []));
                var priceElement = label.QuerySelector(".pl-price");
                var price = Normalize(
                    string.Join(" ", priceElement?.ChildNodes
                        .Where(node => node.NodeType == NodeType.Text)
                        .Select(node => node.TextContent) ?? []));
                var monthly = NormalizeOptional(priceElement?.QuerySelector("span")?.TextContent)?.Trim('~', ' ');
                return new PremiumPlan(
                    match.Groups["method"].Value,
                    days,
                    title,
                    price,
                    monthly,
                    NormalizeOptional(label.QuerySelector(".pl-discount")?.TextContent),
                    popular);
            })
            .Where(plan => plan is not null)
            .Select(plan => plan!)
            .ToList();
        return new PremiumOffers(methods, plans);
    }

    private static PaymentStatus ParseStatus(IElement cell, string label)
    {
        if (cell.ClassList.Contains("green") || label.Contains("усп", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentStatus.Successful;
        }

        if (cell.ClassList.Contains("red") || label.Contains("неуда", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentStatus.Failed;
        }

        if (cell.ClassList.Contains("grey") || label.Contains("обработ", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentStatus.Pending;
        }

        return PaymentStatus.Unknown;
    }

    private static int ParseRequiredInt(string? value, string description)
    {
        var match = IntegerRegex().Match(value ?? "");
        return int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new ParseException($"Could not parse the {description}.");
    }

    private static string Normalize(string? value) =>
        WhitespaceRegex().Replace(value ?? "", " ").Trim();

    private static string? NormalizeOptional(string? value)
    {
        var normalized = Normalize(value);
        return normalized.Length == 0 ? null : normalized;
    }

    [GeneratedRegex(@"^(?<method>.+)-amount$", RegexOptions.IgnoreCase)]
    private static partial Regex PlanNameRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex IntegerRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
