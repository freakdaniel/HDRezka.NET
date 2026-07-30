namespace HdRezka.Abstractions;

internal sealed record StreamSnapshot(
    string Payload,
    string? SubtitleData,
    IReadOnlyDictionary<string, string>? SubtitleLanguages,
    string? DefaultQuality,
    string? DefaultSubtitle,
    Uri? ThumbnailPreview,
    bool IsPremiumContent,
    AccountTier AccountTier);
