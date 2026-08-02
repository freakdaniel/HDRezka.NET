namespace HdRezka;

/// <summary>
/// Identifies a catalog section available on the website home page
/// </summary>
public enum CatalogSection
{
    /// <summary>
    /// Recently added media
    /// </summary>
    Latest,

    /// <summary>
    /// Media currently popular with website users
    /// </summary>
    Popular,

    /// <summary>
    /// Announced media that has not been released yet
    /// </summary>
    Upcoming,

    /// <summary>
    /// Media being watched by website users right now
    /// </summary>
    Watching
}

/// <summary>
/// Describes a media directory assembled from category, genre, year, and ranking filters
/// </summary>
/// <param name="Category">
/// Media category to load
/// </param>
/// <param name="Genre">
/// Website genre slug such as <c>comedy</c>, or <see langword="null"/> to include every genre
/// </param>
/// <param name="Year">
/// Four-digit release year, or <see langword="null"/> to include every year
/// </param>
/// <param name="Best">
/// <see langword="true"/> to use the best-rating directory, otherwise <see langword="false"/> to use the regular category directory
/// </param>
public sealed record CatalogQuery(
    MediaCategory Category,
    string? Genre = null,
    int? Year = null,
    bool Best = false);

/// <summary>
/// Holds one page of website results together with pagination information
/// </summary>
/// <typeparam name="T">
/// Type of item stored on the page
/// </typeparam>
/// <param name="Items">
/// Items in the same order as the website
/// </param>
/// <param name="Page">
/// One-based number of the loaded page
/// </param>
/// <param name="TotalPages">
/// Total page count detected from website navigation
/// </param>
public sealed record PageResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int TotalPages);

/// <summary>
/// Describes one media card shown in catalogs, bookmarks, or collections
/// </summary>
/// <param name="Id">
/// Numeric media identifier, or <see langword="null"/> when the card does not expose one
/// </param>
/// <param name="Title">
/// Media title shown below the cover
/// </param>
/// <param name="Url">
/// Absolute media page URL
/// </param>
/// <param name="ImageUrl">
/// Absolute cover image URL
/// </param>
/// <param name="Category">
/// Media category detected from card markup or URL
/// </param>
/// <param name="Details">
/// Release years, country, and genres shown below the title
/// </param>
/// <param name="Information">
/// Episode or release status shown over the cover, or <see langword="null"/> when absent
/// </param>
public sealed record CatalogItem(
    int? Id,
    string Title,
    Uri Url,
    Uri ImageUrl,
    MediaCategory Category,
    string Details,
    string? Information)
{
    /// <summary>
    /// Gets the internal HDRezka rating shown on the card
    /// </summary>
    /// <value>
    /// Numeric rating, or <see langword="null"/> when the card does not expose it
    /// </value>
    public double? Rating { get; init; }

    /// <summary>
    /// Gets release years parsed from <see cref="Details"/>
    /// </summary>
    public IReadOnlyList<int> Years { get; init; } = [];

    /// <summary>
    /// Gets countries parsed from <see cref="Details"/>
    /// </summary>
    public IReadOnlyList<string> Countries { get; init; } = [];

    /// <summary>
    /// Gets genres parsed from <see cref="Details"/>
    /// </summary>
    public IReadOnlyList<string> Genres { get; init; } = [];

    /// <summary>
    /// Gets whether the website card exposes a trailer action
    /// </summary>
    public bool HasTrailer { get; init; }
}
