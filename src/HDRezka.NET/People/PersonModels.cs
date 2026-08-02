namespace HdRezka;

/// <summary>
/// Describes media associated with one profession on a person page
/// </summary>
/// <param name="Name">
/// Profession name shown by the website
/// </param>
/// <param name="Summary">
/// Film and series count text shown by the website
/// </param>
/// <param name="Items">
/// Media cards associated with this profession
/// </param>
public sealed record PersonCareer(
    string Name,
    string Summary,
    IReadOnlyList<CatalogItem> Items);

/// <summary>
/// Holds complete metadata and filmography from a person page
/// </summary>
/// <param name="Id">
/// Numeric person identifier used by the website
/// </param>
/// <param name="Name">
/// Localized person name
/// </param>
/// <param name="OriginalName">
/// Original person name, or <see langword="null"/> when unavailable
/// </param>
/// <param name="Url">
/// Absolute person page URL
/// </param>
/// <param name="ImageUrl">
/// Absolute portrait URL, or <see langword="null"/> when unavailable
/// </param>
/// <param name="Professions">
/// Profession names shown in the person information table
/// </param>
/// <param name="BirthDateLabel">
/// Birth date or year text shown by the website, or <see langword="null"/> when unavailable
/// </param>
/// <param name="BirthDate">
/// Complete parsed birth date, or <see langword="null"/> when the website exposes only a year or no date
/// </param>
/// <param name="BirthYear">
/// Parsed birth year, or <see langword="null"/> when unavailable
/// </param>
/// <param name="Age">
/// Age reported by the website, or <see langword="null"/> when unavailable
/// </param>
/// <param name="BirthPlace">
/// Birthplace text, or <see langword="null"/> when unavailable
/// </param>
/// <param name="Careers">
/// Filmography grouped by profession
/// </param>
public sealed record Person(
    int Id,
    string Name,
    string? OriginalName,
    Uri Url,
    Uri? ImageUrl,
    IReadOnlyList<string> Professions,
    string? BirthDateLabel,
    DateOnly? BirthDate,
    int? BirthYear,
    int? Age,
    string? BirthPlace,
    IReadOnlyList<PersonCareer> Careers);
