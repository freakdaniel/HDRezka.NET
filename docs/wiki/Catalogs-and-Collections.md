# Catalogs and collections

The catalog and collection clients share the origin, cookies, headers, and
proxy configuration of the main `Client`.

## Home-page sections

The four website sections are available through convenience methods:

```csharp
using HdRezka;

using var client = new Client("https://your-mirror.example");

var latest = await client.Catalog.GetLatestAsync();
var popular = await client.Catalog.GetPopularAsync();
var upcoming = await client.Catalog.GetUpcomingAsync();
var watching = await client.Catalog.GetWatchingAsync();
```

Dedicated website directories are also available:

```csharp
var newReleases = await client.Catalog.GetNewReleasesAsync();
var announcements = await client.Catalog.GetAnnouncementsAsync();
var shows = await client.Catalog.GetShowsAsync();
```

Use `GetPageAsync` when the section is selected dynamically:

```csharp
var page = await client.Catalog.GetPageAsync(
    CatalogSection.Popular,
    MediaCategory.Series,
    page: 2);

Console.WriteLine($"{page.Page}/{page.TotalPages}");

foreach (var item in page.Items)
{
    Console.WriteLine($"{item.Title}: {item.Url}");
}
```

`MediaCategory.Unknown` includes every category. The website category filter
supports films, series, cartoons, and anime. Shows are available through
`GetShowsAsync`. An unsupported category or a page
number below one causes `ArgumentOutOfRangeException`.

## Genre, year, country, and best-rating directories

```csharp
var comedies = await client.Catalog.GetDirectoryAsync(
    new CatalogQuery(
        MediaCategory.Film,
        Genre: "comedy",
        Year: 2025,
        Best: true),
    page: 2);

var usa = await client.Catalog.GetDirectoryAsync(
    "/films/country/usa/",
    page: 1);
```

`CatalogItem` keeps the original `Details` text and also exposes parsed
`Years`, `Countries`, `Genres`, internal `Rating`, and `HasTrailer`.

## Collection directory

```csharp
var directory = await client.Collections.GetPageAsync(page: 1);

foreach (var summary in directory.Items)
{
    Console.WriteLine($"{summary.Title}: {summary.ItemCount}");
}
```

A `CollectionSummary` contains the numeric identifier, title, URL, cover, and
reported item count.

## Collection contents

Load a collection returned by the directory:

```csharp
var summary = directory.Items[0];
var collection = await client.Collections.GetAsync(summary);
```

An absolute URL or a path relative to the configured origin can also be used:

```csharp
var collection = await client.Collections.GetAsync(
    "/collections/123-example/",
    page: 2);
```

`CollectionPage` contains the collection identifier, title, description, URL,
media cards, current page, and detected total page count. Collection media
cards use `CatalogItem`, the same model returned by home-page sections and
bookmark folders.

## Franchises

```csharp
var directory = await client.Franchises.GetPageAsync(page: 1);
var franchise = await client.Franchises.GetAsync(directory.Items[0]);

foreach (var part in franchise.Parts)
{
    Console.WriteLine(
        $"{part.Order}: {part.Title}, {part.Year}, {part.Rating}, {part.Url}");
}
```

Franchise parts are sorted by their website order and include the media ID,
year, rating, and URL when available.

## Pagination and errors

`PageResult<T>` exposes `Items`, `Page`, and `TotalPages`. Page numbers are
one-based.

All network methods accept a `CancellationToken`. They can throw
`LoginRequiredException`, `CaptchaException`, `HttpException`,
`ParseException`, `HttpRequestException`, and `OperationCanceledException`.
Collection methods also throw `ArgumentException` for an invalid collection
URL.
