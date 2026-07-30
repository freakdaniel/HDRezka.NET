namespace HdRezka;

/// <summary>
/// Describes one named link found in media metadata
/// </summary>
/// <param name="Name">
/// Display name shown by the website
/// </param>
/// <param name="Url">
/// Absolute page URL represented by the link
/// </param>
public sealed record NamedLink(string Name, Uri Url);

/// <summary>
/// Describes an actor, director, or another person linked to media
/// </summary>
/// <param name="Id">
/// Numeric person identifier used by the website
/// </param>
/// <param name="Name">
/// Person name shown on the media page
/// </param>
/// <param name="Job">
/// Job shown by the website, such as actor or director
/// </param>
/// <param name="Url">
/// Absolute person page URL
/// </param>
/// <param name="ImageUrl">
/// Absolute portrait URL, or <see langword="null"/> when no portrait is available
/// </param>
public sealed record PersonInfo(
    int Id,
    string Name,
    string Job,
    Uri Url,
    Uri? ImageUrl);

/// <summary>
/// Describes a rating imported by the website from an external service
/// </summary>
/// <param name="Source">
/// Rating source name such as IMDb or Kinopoisk
/// </param>
/// <param name="Value">
/// Numeric rating, or <see langword="null"/> when the value cannot be parsed
/// </param>
/// <param name="Votes">
/// Number of votes, or <see langword="null"/> when the count is unavailable
/// </param>
/// <param name="Url">
/// Website redirect to the external title page, or <see langword="null"/> when no link is available
/// </param>
public sealed record ExternalRating(
    string Source,
    double? Value,
    int? Votes,
    Uri? Url);

/// <summary>
/// Describes one entry from a series release schedule
/// </summary>
/// <param name="Id">
/// Numeric schedule entry identifier used by the website
/// </param>
/// <param name="Season">
/// Season number
/// </param>
/// <param name="Episode">
/// Episode number
/// </param>
/// <param name="Title">
/// Localized episode title, or <see langword="null"/> when unavailable
/// </param>
/// <param name="OriginalTitle">
/// Original episode title, or <see langword="null"/> when unavailable
/// </param>
/// <param name="ReleaseDate">
/// Planned or actual release date, or <see langword="null"/> when it cannot be parsed
/// </param>
/// <param name="IsAvailable">
/// <see langword="true"/> when the website marks the episode as released
/// </param>
/// <param name="IsWatched">
/// <see langword="true"/> when the authenticated account marks the episode as watched
/// </param>
public sealed record EpisodeScheduleEntry(
    long Id,
    int Season,
    int Episode,
    string? Title,
    string? OriginalTitle,
    DateOnly? ReleaseDate,
    bool IsAvailable,
    bool IsWatched);

/// <summary>
/// Holds extended metadata already included in a movie or series page
/// </summary>
/// <param name="Tagline">
/// Media tagline, or <see langword="null"/> when the page does not provide one
/// </param>
/// <param name="ReleaseDate">
/// Full release date, or <see langword="null"/> when it cannot be parsed
/// </param>
/// <param name="Countries">
/// Countries linked by the website
/// </param>
/// <param name="Genres">
/// Genres linked by the website
/// </param>
/// <param name="Directors">
/// Directors listed on the page
/// </param>
/// <param name="Cast">
/// Actors listed on the page
/// </param>
/// <param name="Quality">
/// Quality label shown in media information, or <see langword="null"/> when unavailable
/// </param>
/// <param name="AgeRating">
/// Age restriction text, or <see langword="null"/> when unavailable
/// </param>
/// <param name="Duration">
/// Episode or movie duration, or <see langword="null"/> when it cannot be parsed
/// </param>
/// <param name="Collections">
/// Curated collections linked from the media page
/// </param>
/// <param name="Rankings">
/// Best-of lists and rankings linked from the media page
/// </param>
/// <param name="ExternalRatings">
/// Ratings imported from external services
/// </param>
/// <param name="Recommendations">
/// Related media cards already included in the loaded page
/// </param>
/// <param name="Schedule">
/// Series release schedule, or an empty list for media without a schedule
/// </param>
public sealed record MediaDetails(
    string? Tagline,
    DateOnly? ReleaseDate,
    IReadOnlyList<NamedLink> Countries,
    IReadOnlyList<NamedLink> Genres,
    IReadOnlyList<PersonInfo> Directors,
    IReadOnlyList<PersonInfo> Cast,
    string? Quality,
    string? AgeRating,
    TimeSpan? Duration,
    IReadOnlyList<NamedLink> Collections,
    IReadOnlyList<NamedLink> Rankings,
    IReadOnlyList<ExternalRating> ExternalRatings,
    IReadOnlyList<CatalogItem> Recommendations,
    IReadOnlyList<EpisodeScheduleEntry> Schedule);
