# Series and episodes

Series operations require `media.Format == MediaFormat.Series`, while calling them
for a movie throws `InvalidOperationException`

## Information for one translator

Pass a translator ID or exact name:

```csharp
var info = await media.GetSeriesInfoAsync(
    "56",
    cancellationToken);

foreach (var season in info.Seasons)
{
    Console.WriteLine($"Season {season.Key}: {season.Value}");

    foreach (var episode in info.Episodes[season.Key])
    {
        Console.WriteLine($"  Episode {episode.Key}: {episode.Value}");
    }
}
```

This is the most efficient choice when the application already knows which
translation it needs

## Information for all translators

```csharp
var byTranslator = await media.GetSeriesInfoAsync(cancellationToken);

foreach (var pair in byTranslator)
{
    Console.WriteLine(
        $"{pair.Value.TranslatorName}: " +
        $"{pair.Value.Seasons.Count} seasons");
}
```

The dictionary is indexed by translator ID; when the website exposes variants
with the same ID, the first successfully loaded entry is retained in this
compatibility view

## Merge episodes across translations

Use `GetEpisodesInfoAsync` when the UI needs one catalog with all available
translations attached to every episode:

```csharp
var seasons = await media.GetEpisodesInfoAsync(cancellationToken);

foreach (var season in seasons)
{
    Console.WriteLine($"Season {season.Number}: {season.Title}");

    foreach (var episode in season.Episodes)
    {
        Console.WriteLine($"  Episode {episode.Number}: {episode.Title}");

        foreach (var translation in episode.Translations)
        {
            Console.WriteLine(
                $"    {translation.TranslatorId}: " +
                $"{translation.TranslatorName} " +
                $"(Premium={translation.IsPremium})");
        }
    }
}
```

The all-translator overloads perform explicit catalog-wide aggregation and can
make multiple requests

## Load one episode

```csharp
var stream = await media.GetStreamAsync(
    season: 1,
    episode: 5,
    translation: "56",
    cancellationToken: cancellationToken);
```

If `translation` is omitted, the library selects a translator that contains
the requested season and episode according to priority

## Load a complete season

```csharp
var progress = new Progress<SeasonDownloadProgress>(value =>
    Console.WriteLine($"{value.Completed}/{value.Total}"));

var streams = await media.GetSeasonStreamsAsync(
    season: 1,
    translation: "56",
    progress: progress,
    cancellationToken: cancellationToken);

foreach (var pair in streams)
{
    Console.WriteLine(
        pair.Value is null
            ? $"Episode {pair.Key}: failed"
            : $"Episode {pair.Key}: {pair.Value.Videos.Count} qualities");
}
```

The same translator is used for the entire season, and by default each failed
episode is retried once; after the second failure its dictionary value is
`null`

Set `ignoreErrors: true` to retry each episode until it succeeds or the
operation is canceled:

```csharp
var streams = await media.GetSeasonStreamsAsync(
    season: 1,
    ignoreErrors: true,
    cancellationToken: cancellationToken);
```

Always provide a cancellation token with `ignoreErrors: true`, because a
permanent website or parsing failure would otherwise retry indefinitely

## Caching

Series catalogs are cached for the lifetime of a loaded `Media` instance:

- repeated requests for one translator share its cached load;
- all-translator and merged views reuse translator results;
- stream URLs themselves are fetched per stream request

Create a new `Media` instance when you need a fresh catalog after website data
changes
