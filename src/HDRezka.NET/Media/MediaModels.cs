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
