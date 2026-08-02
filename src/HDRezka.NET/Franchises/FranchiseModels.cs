namespace HdRezka;

/// <summary>
/// Describes one franchise from the website directory
/// </summary>
/// <param name="Id">
/// Numeric franchise identifier
/// </param>
/// <param name="Title">
/// Franchise title shown by the website
/// </param>
/// <param name="Url">
/// Absolute franchise page URL
/// </param>
/// <param name="ImageUrl">
/// Absolute franchise cover URL
/// </param>
/// <param name="PartCount">
/// Number of parts reported by the website
/// </param>
public sealed record FranchiseSummary(
    int Id,
    string Title,
    Uri Url,
    Uri ImageUrl,
    int PartCount);

/// <summary>
/// Describes one ordered media part from a franchise
/// </summary>
/// <param name="Order">
/// Part number shown by the website
/// </param>
/// <param name="MediaId">
/// Numeric media identifier parsed from the media URL, or <see langword="null"/> when unavailable
/// </param>
/// <param name="Title">
/// Media title
/// </param>
/// <param name="Url">
/// Absolute media page URL
/// </param>
/// <param name="Year">
/// Release year, or <see langword="null"/> when unavailable
/// </param>
/// <param name="Rating">
/// Internal HDRezka rating, or <see langword="null"/> when unavailable
/// </param>
public sealed record FranchisePart(
    int Order,
    int? MediaId,
    string Title,
    Uri Url,
    int? Year,
    double? Rating);

/// <summary>
/// Holds an ordered franchise and all of its media parts
/// </summary>
/// <param name="Id">
/// Numeric franchise identifier
/// </param>
/// <param name="Title">
/// Franchise title
/// </param>
/// <param name="Url">
/// Absolute franchise page URL
/// </param>
/// <param name="ImageUrl">
/// Absolute franchise cover URL, or <see langword="null"/> when the detail page does not expose it
/// </param>
/// <param name="Parts">
/// Ordered media parts
/// </param>
public sealed record Franchise(
    int Id,
    string Title,
    Uri Url,
    Uri? ImageUrl,
    IReadOnlyList<FranchisePart> Parts);
