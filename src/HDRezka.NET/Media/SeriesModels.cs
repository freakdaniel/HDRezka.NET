namespace HdRezka;

/// <summary>
/// Describes one translation available for a particular episode
/// </summary>
/// <param name="TranslatorId">
/// Numeric translator identifier used by the website API
/// </param>
/// <param name="TranslatorName">
/// Translator name shown on the media page
/// </param>
/// <param name="IsPremium">
/// <see langword="true"/> when the website marks this translator as premium
/// </param>
public sealed record EpisodeTranslation(int TranslatorId, string TranslatorName, bool IsPremium);

/// <summary>
/// Describes an episode and every translation in which it is available
/// </summary>
/// <param name="Number">
/// Episode number within its season
/// </param>
/// <param name="Title">
/// Episode title shown on the website
/// </param>
/// <param name="Translations">
/// Translations that can provide a stream for this episode
/// </param>
public sealed record Episode(
    int Number,
    string Title,
    IReadOnlyList<EpisodeTranslation> Translations);

/// <summary>
/// Describes a season and its episodes
/// </summary>
/// <param name="Number">
/// Season number used by the website API
/// </param>
/// <param name="Title">
/// Season title shown on the website
/// </param>
/// <param name="Episodes">
/// Episodes ordered by their numbers
/// </param>
public sealed record Season(int Number, string Title, IReadOnlyList<Episode> Episodes);

/// <summary>
/// Holds season and episode identifiers returned for one translator
/// </summary>
/// <param name="TranslatorId">
/// Numeric translator identifier used to request this data
/// </param>
/// <param name="TranslatorName">
/// Translator name shown on the media page
/// </param>
/// <param name="IsPremium">
/// <see langword="true"/> when the website marks this translator as premium
/// </param>
/// <param name="Seasons">
/// Season titles indexed by season number
/// </param>
/// <param name="Episodes">
/// Episode titles indexed first by season number and then by episode number
/// </param>
public sealed record SeriesInfo(
    int TranslatorId,
    string TranslatorName,
    bool IsPremium,
    IReadOnlyDictionary<int, string> Seasons,
    IReadOnlyDictionary<int, IReadOnlyDictionary<int, string>> Episodes);

/// <summary>
/// Reports progress while episode streams are loaded for one season
/// </summary>
/// <param name="Completed">
/// Number of episodes that have finished loading or exhausted their retries
/// </param>
/// <param name="Total">
/// Total number of episodes available through the selected translator
/// </param>
public readonly record struct SeasonDownloadProgress(int Completed, int Total);
