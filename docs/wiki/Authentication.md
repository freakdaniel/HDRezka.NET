# Authentication

Authentication is optional for public media, but it is required for account
state, favorites-protected verification, and Premium content

## Log in

Create the client with an origin and call `LoginAsync`:

```csharp
using var client = new Client("https://your-mirror.example");

var state = await client.LoginAsync(
    "mail@example.com",
    "password",
    rememberMe: true,
    cancellationToken);

Console.WriteLine(state.IsAuthenticated);
Console.WriteLine(state.AccountTier);
Console.WriteLine(string.Join(", ", state.CookieNames));
```

The login flow submits credentials to the compatible website, stores response
cookies in the session, and verifies them against the protected favorites
page, while `CookieNames` is safe for diagnostics because it does not expose cookie
values

`rememberMe: true` requests persistent website cookies but does not save
credentials or cookies to disk on behalf of your application

## Inspect the current state

```csharp
var state = await client.GetAuthenticationStateAsync(cancellationToken);

if (!state.IsAuthenticated)
{
    Console.WriteLine($"Authentication failed at {state.VerificationUri}");
}
```

`AccountTier` can be:

- `Unknown` — the website did not expose a recognizable account marker
- `Standard` — an authenticated account without confirmed Premium access
- `Premium` — an authenticated account with confirmed Premium access

`Unknown` must not be treated as Premium

## Restore existing cookies

If your application already stores persistent authentication values, add them
before constructing the client:

```csharp
var options = new ClientOptions();

foreach (var cookie in AuthenticationCookies.Create(userId, passwordHash))
{
    options.Cookies[cookie.Key] = cookie.Value;
}

using var client = new Client(
    "https://your-mirror.example",
    options);

var state = await client.GetAuthenticationStateAsync(cancellationToken);
```

`AuthenticationCookies.Create` creates the `dle_user_id` and `dle_password`
entries expected by compatible DLE-based websites, so treat both values as
secrets and do not log them or commit them to source control

## Log out

```csharp
var state = await client.LogoutAsync(cancellationToken);
Console.WriteLine(state.IsAuthenticated); // normally false
```

Logout calls the website endpoint, clears known local authentication cookies,
and verifies the resulting state

## Common failures

- `InvalidOperationException` — the client was created without an origin
- `LoginFailedException` — credentials were rejected or the resulting session
  could not be verified
- `CaptchaException` — the website requires interactive captcha verification
- `HttpException` — the website returned an unsuccessful status code
- `ParseException` — the response no longer matches the expected format

See [Error handling](Error-Handling) and
[Troubleshooting](Troubleshooting) for recovery guidance
