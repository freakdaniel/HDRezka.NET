namespace HdRezka;

/// <summary>
/// Describes one curated website collection
/// </summary>
/// <param name="Id">
/// Numeric collection identifier parsed from its URL
/// </param>
/// <param name="Title">
/// Collection title shown on the website
/// </param>
/// <param name="Url">
/// Absolute collection page URL
/// </param>
/// <param name="ImageUrl">
/// Absolute collection cover image URL
/// </param>
/// <param name="ItemCount">
/// Number of media items reported by the website
/// </param>
public sealed record CollectionSummary(
    int Id,
    string Title,
    Uri Url,
    Uri ImageUrl,
    int ItemCount);

/// <summary>
/// Holds one page from a curated collection
/// </summary>
/// <param name="Id">
/// Numeric collection identifier
/// </param>
/// <param name="Title">
/// Collection heading shown on the loaded page
/// </param>
/// <param name="Url">
/// Absolute URL of the loaded collection page
/// </param>
/// <param name="Description">
/// Collection description, or <see langword="null"/> when the page does not provide one
/// </param>
/// <param name="Items">
/// Media cards in the same order as the website
/// </param>
/// <param name="Page">
/// One-based number of the loaded page
/// </param>
/// <param name="TotalPages">
/// Total page count detected from website navigation
/// </param>
public sealed record CollectionPage(
    int Id,
    string Title,
    Uri Url,
    string? Description,
    IReadOnlyList<CatalogItem> Items,
    int Page,
    int TotalPages);
