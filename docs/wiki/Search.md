# Search

Search requires a website origin; use the main `Client` when search should
share cookies, headers, proxy settings, and HTTP connections with media and
authentication requests

## Fast suggestions

```csharp
using var client = new Client("https://your-mirror.example");

var results = await client.SearchAsync(
    "Film name",
    cancellationToken);

foreach (var result in results)
{
    Console.WriteLine(
        $"{result.Title}: {result.Url} ({result.Rating})");
}
```

Fast results contain:

- `Title`
- `Url`
- nullable numeric `Rating`

This endpoint is suitable for autocomplete and small interactive result lists

## One full search page

```csharp
var results = await client.SearchPageAsync(
    "Film name",
    page: 2,
    cancellationToken);

foreach (var result in results)
{
    Console.WriteLine(
        $"{result.Title}: {result.Category}, {result.ImageUrl}");
}
```

Page numbers start at one, while full results contain `Title`, `Url`, `ImageUrl`,
and `Category`

## Search multiple pages

```csharp
var firstTenPages = await client.SearchAllAsync(
    "Film name",
    maximumPages: 10,
    cancellationToken);
```

When `maximumPages` is `null`, the first page determines the total page count
Remaining pages are loaded concurrently up to
`ClientOptions.MaxConcurrentRequests` and returned in page order:

```csharp
var allResults = await client.SearchAllAsync(
    "Film name",
    cancellationToken: cancellationToken);
```

Set a limit when the query can produce many results or when request count
matters

## Standalone search client

```csharp
using var search = new SearchClient(
    "https://your-mirror.example");

var fast = await search.FastSearchAsync("Film name", cancellationToken);
var page = await search.SearchPageAsync("Film name", 1, cancellationToken);
var all = await search.SearchAllAsync("Film name", 5, cancellationToken);
```

A standalone `SearchClient` accepts the same `ClientOptions` and optional
`HttpClient` pattern as the main client; prefer `Client.CreateSearch()` if an
existing session should be shared

## Input validation

- Empty or whitespace-only queries throw `ArgumentException`
- A page number below one throws `ArgumentOutOfRangeException`
- A `maximumPages` value below one throws `ArgumentOutOfRangeException`
