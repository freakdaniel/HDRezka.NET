# Troubleshooting

## A relative URL fails

Relative paths require a client origin:

```csharp
using var client = new Client("https://your-mirror.example");
using var media = await client.GetAsync("/films/drama/123-title.html");
```

If no origin is configured, pass an absolute URL to `Client.GetAsync` or
`Media.CreateAsync`

## Login succeeds on the website but not in the library

1) Confirm that the configured origin is the same mirror that issued the
   cookies
2) Check `AuthenticationState.VerificationUri` and `CookieNames`; never log
   cookie values
3) Check whether the mirror displays captcha, a consent page, or a different
   login form
4) Retry without a proxy or with a consistent proxy for every request
5) If the page markup changed, capture a sanitized reproduction and open an
   issue

## `CaptchaException`

The library does not solve captcha challenges, so complete the interactive check
through an authorized browser session, import the resulting persistent cookies
if appropriate, or wait before retrying

## `ParseException`

A compatible website probably changed markup or endpoint data, so before filing
an issue:

- update to the latest package version;
- identify the operation that failed;
- include the exception message and stack trace;
- include media format and category when known;
- state whether authentication, Premium, custom headers, or a proxy was used;
- remove domains, credentials, cookies, stream URLs, and other sensitive data
  if they should not be public

## No stream is returned

- Confirm that `media.Format` is `Movie` or `Series`, not `Unknown`
- For a series, supply both `season` and `episode`
- Inspect `TranslationOptions` and try an explicit translator ID or exact name
- Check whether the selected translator is Premium-only
- For a series, use `GetEpisodesInfoAsync` to confirm that the translator
  contains the requested episode

## A quality is visible but has no URL

Inspect `stream.Qualities`, not only `stream.Videos`, while a quality with
`RequiresPremium == true` and `IsAvailable == false` is intentionally retained
as metadata while its URLs are withheld; see [Premium access](Premium-Access)

## Proxy settings have no effect

`ClientOptions.Proxy` is used only when the library creates its own
`HttpClient`; if you inject an `HttpClient`, configure the proxy on its handler
as described in [Configuration](Configuration)

## Complete-season loading never finishes

`ignoreErrors: true` retries a failed episode until success or cancellation,
so always pass a cancellation token or timeout:

```csharp
using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));

var streams = await media.GetSeasonStreamsAsync(
    season: 1,
    ignoreErrors: true,
    cancellationToken: timeout.Token);
```

Use the default `ignoreErrors: false` for bounded retry behavior

## Search returns too many requests

Pass `maximumPages` to `SearchAllAsync`; without a limit, the method loads the
total page count detected from the first response

## Reporting a bug

Open an issue at
[github.com/freakdaniel/HDRezka.NET/issues](https://github.com/freakdaniel/HDRezka.NET/issues)
and include a minimal code sample and sanitized diagnostics, but never attach
account passwords, authentication cookie values, or resolved stream URLs
