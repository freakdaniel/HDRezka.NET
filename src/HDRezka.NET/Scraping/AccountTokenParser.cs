using System.Text.Json;
using AngleSharp.Dom;

namespace HdRezka.Scraping;

internal static class AccountTokenParser
{
    public static AccountTier Parse(IDocument document)
    {
        var element = document.QuerySelector("#ctrl_token_id");
        var token = element?.GetAttribute("value") ?? element?.TextContent;
        if (string.IsNullOrWhiteSpace(token))
        {
            return AccountTier.Unknown;
        }

        var segments = token.Split('.');
        if (segments.Length < 2)
        {
            return AccountTier.Unknown;
        }

        try
        {
            var payload = DecodeBase64Url(segments[1]);
            using var json = JsonDocument.Parse(payload);
            if (!json.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("member_id", out var member) ||
                !member.TryGetProperty("is_premium", out var premium))
            {
                return AccountTier.Unknown;
            }

            return ParsePremium(premium);
        }
        catch (Exception exception)
            when (exception is FormatException or JsonException)
        {
            return AccountTier.Unknown;
        }
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized += new string('=', (4 - normalized.Length % 4) % 4);
        return Convert.FromBase64String(normalized);
    }

    private static AccountTier ParsePremium(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.True => AccountTier.Premium,
            JsonValueKind.False => AccountTier.Standard,
            JsonValueKind.Number when value.TryGetInt32(out var number) && number == 1 =>
                AccountTier.Premium,
            JsonValueKind.Number when value.TryGetInt32(out var number) && number == 0 =>
                AccountTier.Standard,
            JsonValueKind.String when value.GetString() == "1" =>
                AccountTier.Premium,
            JsonValueKind.String when value.GetString() == "0" =>
                AccountTier.Standard,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var result) =>
                result ? AccountTier.Premium : AccountTier.Standard,
            _ => AccountTier.Unknown
        };
}
