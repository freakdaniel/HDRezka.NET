using System.Text.Json;
using System.Text.Json.Serialization;
using HdRezka.Abstractions;

namespace HdRezka;

internal sealed class AuthenticationService(
    Uri origin,
    IHttpTransport transport,
    IAuthenticationPageInspector pageInspector)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Uri _origin = new(origin.GetLeftPart(UriPartial.Authority));

    public async Task<AuthenticationState> LoginAsync(
        string email,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var json = await transport.PostFormAsync(
            new Uri(_origin, "/ajax/login/"),
            new Dictionary<string, string>
            {
                ["login_name"] = email.Trim(),
                ["login_password"] = password.Trim(),
                ["login_not_save"] = rememberMe ? "0" : "1"
            },
            cancellationToken,
            _origin).ConfigureAwait(false);
        var response = JsonSerializer.Deserialize<LoginResponse>(json, JsonOptions) ??
            throw new ParseException("The login endpoint returned an empty JSON response.");

        if (!response.Success)
        {
            throw new LoginFailedException(response.Message ?? "Login failed.");
        }

        var state = await GetStateAsync(cancellationToken).ConfigureAwait(false);
        if (!state.IsAuthenticated)
        {
            throw new LoginFailedException(
                "The login endpoint reported success, but the authenticated session could not be verified.");
        }

        return state;
    }

    public async Task<AuthenticationState> GetStateAsync(
        CancellationToken cancellationToken)
    {
        var verificationUri = new Uri(_origin, "/favorites/");
        var html = await transport.GetStringAsync(
            verificationUri,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var page = pageInspector.Inspect(html);
        return new AuthenticationState(
            !page.IsLoginPage,
            verificationUri,
            transport.GetCookieNames(_origin))
        {
            AccountTier = page.AccountTier
        };
    }

    public async Task<AuthenticationState> LogoutAsync(
        CancellationToken cancellationToken)
    {
        await transport.GetStringAsync(
            new Uri(_origin, "/logout/"),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        transport.ClearCookies(
            _origin,
            ["PHPSESSID", "dle_user_id", "dle_password", "dle_hash"]);
        return await GetStateAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed record LoginResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("message")] string? Message);
}
