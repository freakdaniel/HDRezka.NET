namespace HdRezka;

/// <summary>
/// Holds metadata for the account used by the current authenticated session
/// </summary>
/// <param name="Id">
/// Numeric website account identifier
/// </param>
/// <param name="Username">
/// Display name shown in the profile page title
/// </param>
/// <param name="Email">
/// Account email, or <see langword="null"/> when the settings page does not expose it
/// </param>
/// <param name="AvatarUrl">
/// Absolute avatar image URL, or <see langword="null"/> when no avatar is available
/// </param>
/// <param name="Tier">
/// Subscription tier detected from the profile page
/// </param>
/// <param name="ContinueWatchingCount">
/// Number of saved viewing positions reported in the website header
/// </param>
/// <param name="ProfileUrl">
/// Absolute account profile URL
/// </param>
public sealed record AccountProfile(
    int Id,
    string Username,
    string? Email,
    Uri? AvatarUrl,
    AccountTier Tier,
    int ContinueWatchingCount,
    Uri ProfileUrl)
{
    /// <summary>
    /// Gets whether the account has an active Premium subscription
    /// </summary>
    public bool IsPremium => Tier == AccountTier.Premium;
}

/// <summary>
/// Describes one saved viewing position from the continue-watching page
/// </summary>
/// <param name="Id">
/// Numeric save identifier used by the website
/// </param>
/// <param name="Title">
/// Media title shown in viewing history
/// </param>
/// <param name="Url">
/// Absolute media page URL
/// </param>
/// <param name="ImageUrl">
/// Absolute cover image URL
/// </param>
/// <param name="Category">
/// Media category inferred from the media URL
/// </param>
/// <param name="DateLabel">
/// Date text shown by the website, including relative labels such as today or yesterday
/// </param>
/// <param name="Date">
/// Parsed calendar date, or <see langword="null"/> when <paramref name="DateLabel"/> is relative or unrecognized
/// </param>
/// <param name="Details">
/// Release years and completion status shown next to the title
/// </param>
/// <param name="PlaybackInformation">
/// Last season, episode, or translation information shown by the website
/// </param>
/// <param name="Season">
/// Last saved season number, or <see langword="null"/> for movies or unrecognized data
/// </param>
/// <param name="Episode">
/// Last saved episode number, or <see langword="null"/> for movies or unrecognized data
/// </param>
/// <param name="Translator">
/// Last selected translator, or <see langword="null"/> when it cannot be determined
/// </param>
/// <param name="IsWatched">
/// <see langword="true"/> when the website marks this saved position as watched
/// </param>
/// <param name="RemainingEpisodes">
/// Number of later episodes reported by the website, or <see langword="null"/> when unavailable
/// </param>
public sealed record ContinueWatchingEntry(
    long Id,
    string Title,
    Uri Url,
    Uri ImageUrl,
    MediaCategory Category,
    string DateLabel,
    DateOnly? Date,
    string Details,
    string PlaybackInformation,
    int? Season,
    int? Episode,
    string? Translator,
    bool IsWatched,
    int? RemainingEpisodes);

/// <summary>
/// Holds one user-created bookmark folder and its media cards
/// </summary>
/// <param name="Id">
/// Numeric bookmark folder identifier
/// </param>
/// <param name="Name">
/// Folder name shown by the website
/// </param>
/// <param name="ItemCount">
/// Number of bookmarked items reported by the website
/// </param>
/// <param name="Url">
/// Absolute bookmark folder URL
/// </param>
/// <param name="Items">
/// Media cards stored in this folder
/// </param>
public sealed record BookmarkFolder(
    long Id,
    string Name,
    int ItemCount,
    Uri Url,
    IReadOnlyList<CatalogItem> Items);
