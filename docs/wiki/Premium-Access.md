# Premium access

The library distinguishes the account tier detected from the current session
from Premium markers returned for individual resources

## Account tiers

```csharp
var state = await client.GetAuthenticationStateAsync(cancellationToken);

Console.WriteLine(state.AccountTier);
Console.WriteLine(state.IsPremium);
```

`AccountTier` values:

- `Unknown` — the page did not provide enough recognizable information
- `Standard` — the authenticated account does not have confirmed Premium
- `Premium` — the authenticated account has confirmed Premium

`Unknown` is deliberately not treated as Premium, preventing a website markup
change from accidentally unlocking protected URLs

The tier is also attached to loaded objects:

```csharp
Console.WriteLine(media.AccountTier);
Console.WriteLine(media.IsPremiumAccount);
Console.WriteLine(stream.AccountTier);
```

## Premium translations

Premium translations remain visible in metadata:

```csharp
foreach (var translator in media.TranslationOptions)
{
    Console.WriteLine(
        $"{translator.Name}: Premium={translator.IsPremium}");
}
```

Automatic selection skips Premium translations unless the session has
confirmed Premium access, while explicitly selecting a protected translation
without confirmed access throws `PremiumRequiredException` before the player
request

```csharp
try
{
    var stream = await media.GetStreamAsync(
        translation: premiumTranslatorId.ToString(),
        cancellationToken: cancellationToken);
}
catch (PremiumRequiredException exception)
    when (exception.Feature == PremiumFeature.Translation)
{
    Console.WriteLine(exception.Name);
}
```

## Premium qualities

Some player responses include protected qualities such as `1080p Ultra`, `2K`,
or `4K` even for a Standard account, but the library preserves their metadata
and does not expose their URLs:

```csharp
foreach (var quality in stream.Qualities.Values)
{
    Console.WriteLine(
        $"{quality.Name}: " +
        $"RequiresPremium={quality.RequiresPremium}, " +
        $"IsAvailable={quality.IsAvailable}, " +
        $"Urls={quality.Urls.Count}");
}
```

`stream.Videos` contains available qualities only; calling `GetUrls` for a
known protected quality throws `PremiumRequiredException`:

```csharp
try
{
    var urls = stream.GetUrls("4K");
}
catch (PremiumRequiredException exception)
    when (exception.Feature == PremiumFeature.Quality)
{
    Console.WriteLine($"{exception.Name} requires Premium");
}
```

## Protected content

The website can mark the entire stream response as Premium-only, in which case
the exception has `Feature == PremiumFeature.Content`, while
`MediaStream.IsPremiumContent` exposes the returned player marker for a
successfully loaded stream

Never bypass `IsAvailable` or attempt to recover redacted URLs from
`StreamQuality` because the URLs are intentionally omitted for accounts without
confirmed access
