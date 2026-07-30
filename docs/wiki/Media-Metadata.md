# Media metadata

Load media through a reusable `Client`:

```csharp
using var client = new Client("https://your-mirror.example");
using var media = await client.GetAsync(
    "/films/drama/123-title.html",
    cancellationToken);
```

Or load one absolute page directly:

```csharp
using var media = await Media.CreateAsync(
    "https://your-mirror.example/films/drama/123-title.html",
    cancellationToken: cancellationToken);
```

## Core properties

```csharp
Console.WriteLine(media.Id);
Console.WriteLine(media.Url);
Console.WriteLine(media.Origin);
Console.WriteLine(media.Name);
Console.WriteLine(media.OriginalName);
Console.WriteLine(media.Description);
Console.WriteLine(media.ReleaseYear);
Console.WriteLine(media.Thumbnail);
Console.WriteLine(media.ThumbnailHighQuality);
Console.WriteLine(media.Rating.Value);
Console.WriteLine(media.Rating.Votes);
Console.WriteLine(media.Format);
Console.WriteLine(media.Category);
```

`Names` and `OriginalNames` preserve all parsed title variants, where `Name` is the
first localized title, while `OriginalName` is the last original title or
`null`

`MediaFormat` describes the player shape:

- `Movie`
- `Series`
- `Unknown`

`MediaCategory` is inferred from the catalog URL:

- `Film`
- `Series`
- `Cartoon`
- `Anime`
- `Unknown`

Format and category answer different questions; for example, an anime series
has `MediaFormat.Series` and `MediaCategory.Anime`

## Translations

```csharp
foreach (var translator in media.TranslationOptions)
{
    Console.WriteLine(
        $"{translator.Id}: {translator.Name}; " +
        $"Premium={translator.IsPremium}; " +
        $"Camrip={translator.IsCamrip}; " +
        $"Ads={translator.HasAds}; " +
        $"DirectorCut={translator.IsDirectorCut}");
}
```

Use `TranslationOptions` when variants must remain distinct because compatible
websites can return normal and director's-cut entries with the same numeric
identifier

The compatibility views are:

- `Translators` — the first entry for each numeric ID
- `TranslatorsByName` — entries indexed by name without case sensitivity

Sort candidates using the configured priority:

```csharp
var ordered = media.SortTranslators();
```

Or override priority for one operation:

```csharp
var ordered = media.SortTranslators(
    preferred: [111, 56],
    nonPreferred: [238]);
```

See [Configuration](Configuration) for persistent translator settings

## Related parts

`OtherParts` contains related titles in website order:

```csharp
foreach (var part in media.OtherParts)
{
    Console.WriteLine($"{part.Title}: {part.Url}");
}
```

## Account state attached to media

`media.AccountTier` and `media.IsPremiumAccount` describe the session used
when the page was loaded; see [Premium access](Premium-Access) before using
these values to make authorization decisions
