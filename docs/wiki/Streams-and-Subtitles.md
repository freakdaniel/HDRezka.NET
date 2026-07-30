# Streams and subtitles

Load a `Media` instance before requesting a stream:

```csharp
using var client = new Client("https://your-mirror.example");
using var media = await client.GetAsync(mediaPath, cancellationToken);
```

## Movies

For a movie, season and episode are not required:

```csharp
var stream = await media.GetStreamAsync(
    cancellationToken: cancellationToken);
```

Select a translation by numeric ID or exact name:

```csharp
var byId = await media.GetStreamAsync(
    translation: "56",
    cancellationToken: cancellationToken);

var byName = await media.GetStreamAsync(
    translation: "Дубляж",
    cancellationToken: cancellationToken);
```

Without an explicit translation, the library tries candidates in configured
priority order; see [Configuration](Configuration)

## Series

Both season and episode are required:

```csharp
var stream = await media.GetStreamAsync(
    season: 1,
    episode: 5,
    cancellationToken: cancellationToken);
```

See [Series and episodes](Series-and-Episodes) for catalog discovery and
complete-season loading

## Qualities and URLs

`Videos` exposes only qualities currently available:

```csharp
foreach (var pair in stream.Videos)
{
    Console.WriteLine(pair.Key);

    foreach (var url in pair.Value)
    {
        Console.WriteLine(url);
    }
}
```

Compatible player responses can contain a primary URL and one or more
fallbacks that can point to direct MP4 files or HLS playlists

Find an available quality by full label or a case-insensitive fragment:

```csharp
var urls720p = stream.GetUrls("720");
var ultraUrls = stream.GetUrls("Ultra");
```

`Qualities` includes protected metadata as well:

```csharp
foreach (var quality in stream.Qualities.Values)
{
    Console.WriteLine(
        $"{quality.Name}: " +
        $"Premium={quality.RequiresPremium}, " +
        $"Available={quality.IsAvailable}");
}
```

When a quality requires Premium but the current account is not confirmed as
Premium, its `Urls` list is empty, it is excluded from `Videos`, and
`GetUrls` throws `PremiumRequiredException`

## Player metadata

```csharp
Console.WriteLine(stream.Name);
Console.WriteLine(stream.Season);
Console.WriteLine(stream.Episode);
Console.WriteLine(stream.TranslatorId);
Console.WriteLine(stream.DefaultQuality);
Console.WriteLine(stream.DefaultSubtitle);
Console.WriteLine(stream.ThumbnailPreview);
Console.WriteLine(stream.IsPremiumContent);
Console.WriteLine(stream.AccountTier);
```

Optional player fields can be `null` when the website omits them

## Subtitles

Inspect all subtitle tracks:

```csharp
foreach (var subtitle in stream.Subtitles.Items.Values)
{
    Console.WriteLine(
        $"{subtitle.Language}: {subtitle.Title} — {subtitle.Url}");
}
```

Resolve a subtitle by language code, displayed title, or zero-based position:

```csharp
var byCode = stream.Subtitles.GetUrl("en");
var byTitle = stream.Subtitles.GetUrl("English");
var first = stream.Subtitles.GetUrl(0);
```

`GetUrl((string?)null)` returns `null`, while an unknown language or title throws
`ArgumentException`; an invalid position throws `ArgumentOutOfRangeException`

## Override translator order for one call

```csharp
var stream = await media.GetStreamAsync(
    season: 1,
    episode: 5,
    preferred: [111, 56],
    nonPreferred: [238],
    cancellationToken: cancellationToken);
```

The per-call lists do not mutate the media or client defaults
