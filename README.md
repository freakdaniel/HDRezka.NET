<p align="center">
    <img src="./assets/logo.png" width="256" height="256"/>
</p>

<p align="center">
    <i>
        An asynchronous .NET 10 library for working with HDRezka 
        that can use sessions, load account, catalog, comment, and media data,
        enumerate translations and episodes, resolve video streams and subtitles
    </i>
</p>

<p align="center">
    <a href="https://www.nuget.org/packages/HDRezka.NET">
        <img alt="NuGet Version" src="https://img.shields.io/nuget/v/HDRezka.NET?style=flat&link=https%3A%2F%2Fwww.nuget.org%2Fpackages%2FHDRezka.NET">
    </a>
</p>

> [!IMPORTANT]
> The website has multiple mirrors and can change its markup or API without
> notice, so pass the mirror URL you are allowed to access; this package does not
> contain a hard-coded domain


> [!NOTE]
> The project was originally a fork of https://github.com/SuperZombi/HdRezkaApi intended to create a library for .NET, but it ultimately became completely self-sufficient and superior to the original in every aspect

## Requirements

- .NET 10 SDK

## Installation

```shell
dotnet add package HDRezka.NET
```

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
Console.WriteLine(media.Details.Duration);

foreach (var genre in media.Details.Genres)
{
    Console.WriteLine(genre.Name);
}

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

`Details` also exposes the full release date, countries, genres, directors,
cast, quality, age rating, tagline, external ratings, collections, rankings,
recommendations, and series schedule when present

`media.Rating` is the aggregate rating submitted by HDRezka users. Ratings
imported from IMDb, Kinopoisk, and other services are available separately
through `media.Details.ExternalRatings`

All network methods accept a `CancellationToken`

## Comments

Comments use the website AJAX endpoint, so the complete media page is not
downloaded again:

```csharp
var comments = await media.Comments.GetPageAsync(page: 1);

foreach (var comment in comments.Items)
{
    Console.WriteLine($"{comment.Author}: {comment.Text}");
}

var created = await media.Comments.AddAsync(
    "A detailed review that follows the website rules");
var reply = await media.Comments.ReplyAsync(
    created.Id,
    "A reply to the published comment");
await media.Comments.DeleteAsync(reply.Id);
var like = await media.Comments.ToggleLikeAsync(comments.Items[0].Id);
var likedBy = await media.Comments.GetLikeUsersAsync(comments.Items[0].Id);
```

Comment creation, replies, and deletion require an authenticated account.
The website does not expose comment editing, so the library does not emulate
it by deleting and reposting content

## Catalogs, people, franchises, and trailers

```csharp
var catalog = await session.Catalog.GetDirectoryAsync(
    new CatalogQuery(MediaCategory.Film, "comedy", 2025, Best: true));

var person = await session.People.GetAsync(media.Details.Cast[0]);

var franchiseDirectory = await session.Franchises.GetPageAsync();
var franchise = await session.Franchises.GetAsync(franchiseDirectory.Items[0]);

var trailer = await media.GetTrailerAsync();
var relatedMedia = await media.GetOtherPartsAsync();
```

Catalog cards expose structured years, countries, genres, card rating, and
trailer availability. Person pages include biography and grouped filmography,
while franchise parts include their order, year, rating, media ID, and URL

## Internal rating

The current aggregate HDRezka rating is loaded with the media page. An
authenticated account can submit one integer score from 1 through 10:

```csharp
Console.WriteLine($"{media.Rating.Value} from {media.Rating.Votes} votes");

var updatedRating = await media.RateAsync(9);
Console.WriteLine($"{updatedRating.Value} from {updatedRating.Votes} votes");
```

The website can reject repeated voting by the same account. A rejected vote
throws `RatingException`

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

Episodes are loaded concurrently using `ClientOptions.MaxConcurrentRequests`.
Each failed episode is retried once, after which its result is `null` on
another failure. Set `ignoreErrors: true` to keep retrying until success or
cancellation

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

`maximumPages` is optional. The first page determines the available page count,
after which remaining pages are loaded concurrently and returned in page order

A standalone search client is also available:

```csharp
using var search = new SearchClient("https://your-mirror.example");
var results = await search.FastSearchAsync("Film name");
```

## Account data

Account operations share the authenticated session:

```csharp
var profile = await session.Account.GetProfileAsync();

Console.WriteLine(profile.Username);
Console.WriteLine(profile.AvatarUrl);
Console.WriteLine(profile.Tier);
Console.WriteLine(profile.ContinueWatchingCount);

var continueWatching = await session.Account.GetContinueWatchingAsync();
var bookmarkFolders = await session.Account.GetBookmarksAsync();
```

Continue-watching entries expose the saved date, cover, media category, season,
episode, translator, watched state, and remaining episode count when available

Bookmarks preserve user-created folders and return their media as `CatalogItem`
instances

Playback progress can be synchronized after loading a stream

```csharp
using var media = await session.GetAsync(
    "/series/drama/66689-title.html");
var stream = await media.GetStreamAsync(season: 1, episode: 4);

await session.Account.SavePlaybackProgressAsync(
    new PlaybackProgress(
        media.Id,
        stream.TranslatorId,
        stream.Season,
        stream.Episode,
        Position: TimeSpan.FromMinutes(18),
        Duration: TimeSpan.FromMinutes(52)));
```

The library resolves streams but does not play them, so the application reports
the current position when playback starts, pauses, seeks, or closes

Continue-watching and bookmark mutations use the same authenticated session

```csharp
var entry = continueWatching[0];
entry = await session.Account.SetContinueWatchingWatchedAsync(
    entry,
    isWatched: true);

var folder = await session.Account.CreateBookmarkFolderAsync("Watch later");
await media.SetBookmarkAsync(folder.Id, isBookmarked: true);
await session.Account.RemoveContinueWatchingAsync(entry.Id);
await session.Account.DeleteBookmarkFolderAsync(folder.Id);
```

`Media.BookmarkFolderIds` contains the selected sections from the loaded media
page, so `SetBookmarkAsync` sends no request when the requested state already
matches

Deleting a bookmark folder also deletes every bookmark it contains

Password and avatar changes use the same authenticated session:

```csharp
await session.Account.ChangePasswordAsync(
    currentPassword: "current-password",
    newPassword: "new-password");

await using var avatar = File.OpenRead("avatar.png");
var avatarResult = await session.Account.SetAvatarAsync(
    avatar,
    "avatar.png");
```

The website requires passwords with at least eight characters. Avatar upload
is confirmed for PNG and JPEG images with dimensions of at least 60 by 60
pixels. By default the library applies the largest centered square crop; pass
an `AvatarCrop` to select another square in original image coordinates

## Catalog sections

The four home-page sections and their category filters are available through
`Catalog`:

```csharp
var latest = await session.Catalog.GetLatestAsync();
var popularSeries = await session.Catalog.GetPopularAsync(
    MediaCategory.Series,
    page: 2);
var upcoming = await session.Catalog.GetUpcomingAsync();
var watchingNow = await session.Catalog.GetWatchingAsync();
var newReleases = await session.Catalog.GetNewReleasesAsync();
var announcements = await session.Catalog.GetAnnouncementsAsync();
var shows = await session.Catalog.GetShowsAsync();
```

Every result contains the current page, detected total page count, and media
cards with title, cover, category, details, and episode or release information

## Collections

```csharp
var directory = await session.Collections.GetPageAsync();
var firstCollection = directory.Items[0];
var collection = await session.Collections.GetAsync(firstCollection);

foreach (var item in collection.Items)
{
    Console.WriteLine($"{item.Title}: {item.Url}");
}
```

Both the collection directory and collection contents support one-based
pagination

## Authentication, cookies, headers, and proxy

Credential-based login reproduces the website flow: it sends
`POST /ajax/login/`, stores the returned session cookies in a real
`CookieContainer`, and verifies the session against `/favorites/`

```csharp
var options = new ClientOptions();
options.Headers["X-Custom-Header"] = "value";
options.Proxy = new WebProxy("http://127.0.0.1:8080");
options.MaxConcurrentRequests = 4;

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

## HTTP and parsing

The library does not run a browser and does not read a browser DOM. Every
operation uses `HttpClient`:

- JSON or compact HTML endpoints are used directly for login, player data,
  seasons, fast search, and comments
- regular pages are downloaded as HTML and parsed in memory with AngleSharp
- bulk page, translator, bookmark, and episode requests use limited
  asynchronous concurrency without creating dedicated threads

## Architecture

Production code is compiled into one `HDRezka.NET` assembly, while logical
responsibilities remain separated by directories without introducing extra
projects or NuGet dependencies:

- `Client`: client entry point, options, authentication state, and cookie helpers
- `Account`: profile metadata and changes, continue-watching history, and bookmarks
- `Catalog`: home-page catalog sections and shared media cards
- `Collections`: curated collection directory and content
- `Comments`: loading, creation, replies, and deletion
- `Media`: media facade, internal ratings, streams, subtitles, translators, seasons, and episodes
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
- `AccountUpdateException`
- `CommentOperationException`
- `RatingException`
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
