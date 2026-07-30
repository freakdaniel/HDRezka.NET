using AngleSharp.Html.Parser;
using HdRezka.Abstractions;

namespace HdRezka.Scraping;

internal sealed class AuthenticationPageInspector : IAuthenticationPageInspector
{
    public AuthenticationPageSnapshot Inspect(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        var title = (document.Title ?? "").Trim();
        var isLoginPage = title.Equals("Sign In", StringComparison.OrdinalIgnoreCase) ||
            title.Equals("Вход", StringComparison.OrdinalIgnoreCase) ||
            document.QuerySelector("form[action=\"/ajax/login/\"]") is not null;
        return new AuthenticationPageSnapshot(
            isLoginPage,
            isLoginPage ? AccountTier.Unknown : AccountTokenParser.Parse(document));
    }
}
