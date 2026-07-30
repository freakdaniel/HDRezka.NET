namespace HdRezka.Abstractions;

internal interface IAuthenticationPageInspector
{
    AuthenticationPageSnapshot Inspect(string html);
}
