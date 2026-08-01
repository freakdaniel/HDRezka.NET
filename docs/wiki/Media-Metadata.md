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

`Rating` is the internal aggregate score submitted by HDRezka users, not an
external service score. `Rating.Value` and `Rating.Votes` are nullable when the
page has no recognizable rating. External scores remain available through
`Details.ExternalRatings`.

An authenticated account can submit an integer score from 1 through 10:

```csharp
var updated = await media.RateAsync(9, cancellationToken);
Console.WriteLine($"{updated.Value} ({updated.Votes})");
```

The method also updates `media.Rating`. The website can reject a repeated vote
from the same account, which is reported as `RatingException`.

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
- `Show`
- `Unknown`

Format and category answer different questions; for example, an anime series
has `MediaFormat.Series` and `MediaCategory.Anime`

## Extended details

`Details` contains metadata that is already present in the downloaded media
page and therefore requires no additional request:

```csharp
Console.WriteLine(media.Details.Tagline);
Console.WriteLine(media.Details.ReleaseDate);
Console.WriteLine(media.Details.Quality);
Console.WriteLine(media.Details.AgeRating);
Console.WriteLine(media.Details.Duration);

foreach (var country in media.Details.Countries)
{
    Console.WriteLine(country.Name);
}

foreach (var person in media.Details.Cast)
{
    Console.WriteLine($"{person.Name}: {person.Url}");
}
```

The remaining collections expose genres, directors, linked collections,
rankings, external ratings, recommendations, and series schedule entries.
Nullable values indicate that the compatible website omitted the value or used
an unrecognized format

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

## Comments

Comments are loaded separately through the website AJAX endpoint:

```csharp
var page = await media.Comments.GetPageAsync(page: 1);

var created = await media.Comments.AddAsync(
    "A detailed review that follows the website rules",
    cancellationToken);
var reply = await media.Comments.ReplyAsync(
    created.Id,
    "A reply to the review",
    cancellationToken);
await media.Comments.DeleteAsync(reply.Id, cancellationToken);
```

`CommentPage` contains nested comments in website order, current and total page
numbers, and the latest update identifier. Each comment exposes its parent
identifier, nesting depth, author, avatar, date label, text, like count, and
permalink

Creation, replies, and deletion require an authenticated account and can be
rejected by moderation, captcha, ownership checks, or comment rules. The live
website exposes deletion for a user's own comments but does not expose comment
editing, so the library intentionally has no edit method.
