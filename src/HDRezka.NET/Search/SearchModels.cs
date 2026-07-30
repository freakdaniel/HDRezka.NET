namespace HdRezka;

/// <summary>
/// Holds one result returned by the compact suggestion endpoint
/// </summary>
/// <param name="Title">
/// Media title shown in the suggestion
/// </param>
/// <param name="Url">
/// Absolute media page URL
/// </param>
/// <param name="Rating">
/// Numeric rating shown in the suggestion, or <see langword="null"/> when unavailable
/// </param>
public sealed record FastSearchResult(string Title, Uri Url, double? Rating);

/// <summary>
/// Holds one result returned by the full website search
/// </summary>
/// <param name="Title">
/// Media title shown in the result
/// </param>
/// <param name="Url">
/// Absolute media page URL
/// </param>
/// <param name="ImageUrl">
/// Absolute thumbnail URL
/// </param>
/// <param name="Category">
/// Catalog category inferred from the result markup or URL
/// </param>
public sealed record SearchResult(
    string Title,
    Uri Url,
    Uri ImageUrl,
    MediaCategory Category);
