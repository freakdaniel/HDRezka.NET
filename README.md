<p align="center">
    <img src="./assets/logo.png" width="256" height="256"/>
</p>

<p align="center">
    <i>
        An asynchronous .NET 10 library for working with HDRezka 
        that can use sessions, load media metadata, enumerate translations and episodes, resolve video
        streams and subtitles
    </i>
</p>

<p align="center">
    <img alt="NuGet Version" src="https://img.shields.io/nuget/v/HDRezka.NET?style=flat&link=https%3A%2F%2Fwww.nuget.org%2Fpackages%2FHDRezka.NET">
</p>

> [!NOTE]
> The website has multiple mirrors and can change its markup or API without
> notice, so pass the mirror URL you are allowed to access; this package does not
> contain a hard-coded domain

## Requirements

- .NET 10 SDK

## Installation

```shell
dotnet add package HDRezka.NET
```

The package uses the `HdRezka` namespace

## Docs

For task-oriented guides and troubleshooting, see the
[project wiki](https://github.com/freakdaniel/HDRezka.NET/wiki), published
automatically from [`docs/wiki`](docs/wiki) on `main`

## Load a movie or series

Use a session when performing more than one request:

```csharp
using HdRezka;

using var session = new Client("https://your-mirror.example");
var authentication = await session.LoginAsync(
    "mail@example.com",
    "password",
    rememberMe: true);

Console.WriteLine(authentication.IsAuthenticated);
Console.WriteLine(authentication.AccountTier);
using var media = await session.GetAsync("/films/drama/123-title.html");

Console.WriteLine(media.Name);
Console.WriteLine(media.Description);
Console.WriteLine(media.Thumbnail);
Console.WriteLine(media.Rating.Value);
Console.WriteLine(media.Rating.Votes);
Console.WriteLine(media.Format);
Console.WriteLine(media.Category);

foreach (var translator in media.TranslationOptions)
{
    Console.WriteLine(
        $"{translator.Id}: {translator.Name}, Premium: {translator.IsPremium}");
}
```

`TranslationOptions` preserves every website entry even when normal and
director's-cut variants share the same numeric ID, while `Translators` remains
available as a compatibility view containing the first variant for each ID

For a single page, a session is optional:

```csharp
using var media = await Media.CreateAsync(
    "https://your-mirror.example/films/drama/123-title.html");
```

All network methods accept a `CancellationToken`

## Streams

For a movie:

```csharp
var stream = await media.GetStreamAsync();
var hdUrls = stream.GetUrls("720");

foreach (var url in hdUrls)
{
    Console.WriteLine(url);
}
```

The returned URLs can contain direct MP4 files and HLS playlists, while player
metadata also exposes the default quality, default subtitle, timeline preview,
and premium-content marker when the website returns them

## HDRezka Premium

The subscription tier is detected from the authenticated page:

```csharp
var state = await session.GetAuthenticationStateAsync();

Console.WriteLine(state.AccountTier);
Console.WriteLine(state.IsPremium);
Console.WriteLine(media.IsPremiumAccount);
```

`AccountTier.Unknown` means that the website did not provide a recognizable
account token and is not treated as proof of Premium access

Premium translations remain visible through `TranslationOptions`, while automatic
selection skips them on a standard account and selecting one explicitly throws
`PremiumRequiredException` before the player request is sent

The player can return `1080p Ultra`, `2K`, and `4K` entries to a standard
account together with protected URLs, but the library keeps those entries as
metadata but does not expose their URLs:

```csharp
foreach (var quality in stream.Qualities.Values)
{
    Console.WriteLine(
        $"{quality.Name}: Premium={quality.RequiresPremium}, Available={quality.IsAvailable}");
}

var availableUrls = stream.Videos;
```

`Videos` contains only available qualities, while calling `GetUrls("4K")` without
confirmed Premium access throws `PremiumRequiredException`

For a series:

```csharp
var stream = await media.GetStreamAsync(season: 1, episode: 5);
```

A translation can be selected by ID or exact name:

```csharp
var byId = await media.GetStreamAsync(1, 5, translation: "56");
var byName = await media.GetStreamAsync(1, 5, translation: "Дубляж");
```

Without an explicit translation, the configured priority is used with defaults
are `56`, `105`, and `111`; translator `238` is non-preferred

```csharp
media.PreferredTranslators.Clear();
media.PreferredTranslators.Add(111);
media.NonPreferredTranslators.Add(999);
```

Load every episode stream from one season:

```csharp
var progress = new Progress<SeasonDownloadProgress>(value =>
    Console.WriteLine($"{value.Completed}/{value.Total}"));

var streams = await media.GetSeasonStreamsAsync(
    season: 1,
    progress: progress,
    cancellationToken: cancellationToken);
```

Each failed episode is retried once, after which the result for
that episode is `null` on another failure Set `ignoreErrors: true` to keep retrying until success
or cancellation

## Seasons and episodes

Data for one translator:

```csharp
var info = await media.GetSeriesInfoAsync("56");
```

Data for every translator:

```csharp
var infoByTranslator = await media.GetSeriesInfoAsync();
```

Merged seasons and episodes:

```csharp
var seasons = await media.GetEpisodesInfoAsync();

foreach (var season in seasons)
{
    foreach (var episode in season.Episodes)
    {
        Console.WriteLine($"S{season.Number}E{episode.Number}: {episode.Title}");
        foreach (var translation in episode.Translations)
        {
            Console.WriteLine($"  {translation.TranslatorId}: {translation.TranslatorName}");
        }
    }
}
```

Results are cached for the lifetime of the loaded `Media` instance, while loading one
stream or season queries only the selected translator and the all-translator
overloads perform the explicit catalog-wide aggregation

## Subtitles

```csharp
var stream = await media.GetStreamAsync(1, 5);

Console.WriteLine(string.Join(", ", stream.Subtitles.Languages));
var english = stream.Subtitles.GetUrl("en");
var byTitle = stream.Subtitles.GetUrl("English");
var first = stream.Subtitles.GetUrl(0);
```

`GetUrl(string)` accepts either a language code or a subtitle title

## Search

Fast AJAX search:

```csharp
var results = await session.SearchAsync("Film name");
foreach (var result in results)
{
    Console.WriteLine($"{result.Title}: {result.Url} ({result.Rating})");
}
```

Full search:

```csharp
var page = await session.SearchPageAsync("Film name", page: 2);
var all = await session.SearchAllAsync("Film name", maximumPages: 10);
```

`maximumPages` is optional and, when omitted, pages are loaded until the website
returns an empty page

A standalone search client is also available:

```csharp
using var search = new SearchClient("https://your-mirror.example");
var results = await search.FastSearchAsync("Film name");
```

## Authentication, cookies, headers, and proxy

Credential-based login reproduces the website flow: it sends
`POST /ajax/login/`, stores the returned session cookies in a real
`CookieContainer`, and verifies the session against `/favorites/`

```csharp
var options = new ClientOptions();
options.Headers["X-Custom-Header"] = "value";
options.Proxy = new WebProxy("http://127.0.0.1:8080");

using var session = new Client(
    "https://your-mirror.example",
    options);

var login = await session.LoginAsync(
    "mail@example.com",
    "password",
    rememberMe: true);

if (!login.IsAuthenticated)
{
    throw new InvalidOperationException("Login was not verified.");
}

var current = await session.GetAuthenticationStateAsync();
var logout = await session.LogoutAsync();
```

`rememberMe: true` maps to the website's `login_not_save=0` behavior, while the
authentication result exposes cookie names for diagnostics, but never cookie
values

Existing authentication cookies can still be imported explicitly when
restoring a previously saved session:

```csharp
foreach (var cookie in AuthenticationCookies.Create(userId, passwordHash))
{
    options.Cookies[cookie.Key] = cookie.Value;
}
```

When supplying your own `HttpClient`, configure its handler if you need a
proxy, while compressed responses are handled by the library and the injected
client is never disposed

## Architecture

Production code is compiled into one `HDRezka.NET` assembly with logical
responsibilities remain separated by directories without introducing extra
projects or NuGet dependencies:

- `Client`: client entry point, options, authentication state, and cookie helpers
- `Media`: media facade, streams, subtitles, translators, seasons, and episodes
- `Search`: search client and result models
- `Exceptions`: public library exceptions
- `Abstractions`: internal contracts shared by the client and parsers
- `Http`: HTTP transport, `CookieContainer`, and response decompression
- `Scraping`: AngleSharp page parsing and authentication page inspection
- `Translators`: automatic translator ordering and selection

All public types remain in the single `HdRezka` namespace regardless of their
feature directory

## Exceptions

Library-specific failures derive from `ApiException`:

- `LoginRequiredException`
- `LoginFailedException`
- `PremiumRequiredException`
- `CaptchaException`
- `StreamFetchException`
- `HttpException`
- `ParseException`

Invalid method arguments use standard .NET exceptions such as
`ArgumentException`, `ArgumentOutOfRangeException`, and
`InvalidOperationException`

## Build, test, and pack

```shell
dotnet build HDRezka.NET.slnx --configuration Release
dotnet test HDRezka.NET.slnx --configuration Release
dotnet pack src/HDRezka.NET/HDRezka.NET.csproj \
  --configuration Release \
  --output artifacts
```

The live integration test is opt-in and does not store credentials:

```shell
HDREZKA_TEST_EMAIL="mail@example.com" \
HDREZKA_TEST_PASSWORD="password" \
HDREZKA_TEST_ORIGIN="https://your-mirror.example" \
dotnet test tests/HDRezka.NET.IntegrationTests \
  --configuration Release \
  --filter "Category=Live"
```
