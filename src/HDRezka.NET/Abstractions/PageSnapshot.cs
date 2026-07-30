namespace HdRezka.Abstractions;

internal sealed record PageSnapshot(
    int Id,
    string Name,
    IReadOnlyList<string> Names,
    string? OriginalName,
    IReadOnlyList<string> OriginalNames,
    string Description,
    Uri Thumbnail,
    Uri? ThumbnailHighQuality,
    int? ReleaseYear,
    MediaFormat Format,
    MediaCategory Category,
    Rating Rating,
    AccountTier AccountTier,
    IReadOnlyList<Translator> TranslationOptions,
    IReadOnlyDictionary<int, Translator> Translators,
    IReadOnlyDictionary<string, Translator> TranslatorsByName,
    IReadOnlyList<RelatedPart> OtherParts,
    string Favorites,
    SeriesInfo? InitialSeriesInfo);
