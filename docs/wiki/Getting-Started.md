# Getting started

## Install

Add the package to a .NET 10 project:

```shell
dotnet add package HDRezka.NET
```

Import the namespace:

```csharp
using HdRezka;
```

## Create a reusable client

Use one `Client` for related operations and shared cookies, headers, HTTP
connections, and authentication state:

```csharp
using HdRezka;

using var client = new Client("https://your-mirror.example");
using var media = await client.GetAsync(
    "/films/drama/123-title.html",
    cancellationToken);

Console.WriteLine(media.Name);
Console.WriteLine(media.ReleaseYear);
Console.WriteLine(media.Format);
Console.WriteLine(media.Category);
Console.WriteLine(media.Rating);
```

A configured origin makes relative media paths possible and is also required
for authentication and search

## Resolve a movie stream

```csharp
var stream = await media.GetStreamAsync(
    cancellationToken: cancellationToken);

foreach (var quality in stream.Videos)
{
    Console.WriteLine(quality.Key);

    foreach (var url in quality.Value)
    {
        Console.WriteLine($"  {url}");
    }
}
```

`Videos` contains only qualities available to the current account; see
[Streams and subtitles](Streams-and-Subtitles) for quality selection and
[Premium access](Premium-Access) for protected streams

## Load one page without a client

For a single absolute URL, use `Media.CreateAsync`:

```csharp
using var media = await Media.CreateAsync(
    "https://your-mirror.example/films/drama/123-title.html",
    cancellationToken: cancellationToken);
```

Create a reusable `Client` when you need more than one request, authentication,
search, custom cookies, or shared connection settings

## Lifetime and cancellation

`Client`, `Media`, and standalone `SearchClient` instances implement
`IDisposable`, so dispose instances that you create; a `Media` loaded through a
`Client` shares the client's transport, so keep the client alive while using
that media instance

Pass a `CancellationToken` to network calls:

```csharp
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
using var media = await client.GetAsync(path, timeout.Token);
```

Cancellation is reported as `OperationCanceledException`

## Next steps

- [Authentication](Authentication)
- [Media metadata](Media-Metadata)
- [Streams and subtitles](Streams-and-Subtitles)
- [Configuration](Configuration)
