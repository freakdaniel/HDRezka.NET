namespace HdRezka;

/// <summary>
/// Identifies whether a media page represents a movie or a series
/// </summary>
public enum MediaFormat
{
    /// <summary>
    /// The page does not expose a recognized media format
    /// </summary>
    Unknown,

    /// <summary>
    /// The page represents a series with seasons and episodes
    /// </summary>
    Series,

    /// <summary>
    /// The page represents a single movie
    /// </summary>
    Movie
}

/// <summary>
/// Identifies the catalog category inferred from a media or search result URL
/// </summary>
public enum MediaCategory
{
    /// <summary>
    /// The URL does not contain a recognized category
    /// </summary>
    Unknown,

    /// <summary>
    /// A live-action film
    /// </summary>
    Film,

    /// <summary>
    /// A live-action series
    /// </summary>
    Series,

    /// <summary>
    /// An animated film or series outside the anime category
    /// </summary>
    Cartoon,

    /// <summary>
    /// An anime film or series
    /// </summary>
    Anime,

    /// <summary>
    /// A television program or entertainment show
    /// </summary>
    Show
}

/// <summary>
/// Identifies whether the website currently provides a player for media
/// </summary>
public enum PlaybackAvailability
{
    /// <summary>
    /// The player and at least one translation are available
    /// </summary>
    Available,

    /// <summary>
    /// The website reports that playback is temporarily unavailable or being restored
    /// </summary>
    TemporarilyUnavailable,

    /// <summary>
    /// The website does not provide a player for this media
    /// </summary>
    Unavailable
}

/// <summary>
/// Describes current player availability reported by the media page
/// </summary>
/// <param name="Availability">
/// Current player availability
/// </param>
/// <param name="Reason">
/// Human-readable website message, or <see langword="null"/> when playback is available or no message is provided
/// </param>
public sealed record PlaybackState(
    PlaybackAvailability Availability,
    string? Reason)
{
    /// <summary>
    /// Gets whether stream and episode operations can be requested
    /// </summary>
    public bool IsAvailable => Availability == PlaybackAvailability.Available;
}

/// <summary>
/// Holds a rating and its vote count when the page provides them
/// </summary>
/// <param name="Value">
/// Numeric rating, or <see langword="null"/> when no rating is available
/// </param>
/// <param name="Votes">
/// Number of submitted votes, or <see langword="null"/> when no vote count is available
/// </param>
public sealed record Rating(double? Value, int? Votes)
{
    /// <summary>
    /// Gets whether a numeric rating is available
    /// </summary>
    public bool HasValue => Value.HasValue;

    /// <summary>
    /// Returns the rating and vote count in a readable form
    /// </summary>
    /// <returns>
    /// Rating followed by its vote count, or <c>Rating(Empty)</c> when no value is available
    /// </returns>
    public override string ToString() =>
        HasValue ? $"{Value} ({Votes})" : "Rating(Empty)";
}

/// <summary>
/// Describes one voice-over or subtitle translation available for media
/// </summary>
/// <param name="Id">
/// Numeric translator identifier used by the website API
/// </param>
/// <param name="Name">
/// Translator name shown on the media page
/// </param>
/// <param name="IsPremium">
/// <see langword="true"/> when the website marks this translator as premium
/// </param>
public sealed record Translator(int Id, string Name, bool IsPremium)
{
    /// <summary>
    /// Gets whether the website marks this translation as recorded from a cinema
    /// </summary>
    public bool IsCamrip { get; init; }

    /// <summary>
    /// Gets whether the website marks this translation as containing advertising
    /// </summary>
    public bool HasAds { get; init; }

    /// <summary>
    /// Gets whether the website marks this translation as a director's cut
    /// </summary>
    public bool IsDirectorCut { get; init; }
}

/// <summary>
/// Describes another part related to the current media title
/// </summary>
/// <param name="Title">
/// Part title shown on the media page
/// </param>
/// <param name="Url">
/// Absolute URL of the related part
/// </param>
public sealed record RelatedPart(string Title, Uri Url);

/// <summary>
/// Describes trailer metadata returned by the website
/// </summary>
/// <param name="Title">
/// Trailer title, or an empty string when the website does not provide one
/// </param>
/// <param name="Description">
/// Trailer description, or an empty string when the website does not provide one
/// </param>
/// <param name="EmbedHtml">
/// Raw player markup returned by the trailer endpoint
/// </param>
/// <param name="SourceUrl">
/// Direct iframe, video, or source URL parsed from <paramref name="EmbedHtml"/>, or <see langword="null"/> when the markup uses inline script
/// </param>
/// <param name="MediaUrl">
/// Absolute link back to the full media page, or <see langword="null"/> when unavailable
/// </param>
public sealed record Trailer(
    string Title,
    string Description,
    string EmbedHtml,
    Uri? SourceUrl,
    Uri? MediaUrl);
