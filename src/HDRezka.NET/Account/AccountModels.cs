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
/// Defines a square avatar crop in pixels of the original uploaded image
/// </summary>
/// <param name="X">
/// Horizontal offset of the crop from the left image edge
/// </param>
/// <param name="Y">
/// Vertical offset of the crop from the top image edge
/// </param>
/// <param name="Size">
/// Width and height of the square crop
/// </param>
public sealed record AvatarCrop(int X, int Y, int Size);

/// <summary>
/// Describes an avatar accepted and cropped by the website
/// </summary>
/// <param name="AvatarUrl">
/// Absolute URL of the generated 60 by 60 pixel avatar
/// </param>
/// <param name="SourceWidth">
/// Width of the uploaded source image in pixels
/// </param>
/// <param name="SourceHeight">
/// Height of the uploaded source image in pixels
/// </param>
/// <param name="Crop">
/// Crop applied in coordinates of the original source image
/// </param>
public sealed record AvatarUpdateResult(
    Uri AvatarUrl,
    int SourceWidth,
    int SourceHeight,
    AvatarCrop Crop);

/// <summary>
/// Describes an account setting change accepted by the website
/// </summary>
/// <param name="Message">
/// Confirmation text returned by the website, or an empty string when no text was provided
/// </param>
public sealed record AccountUpdateResult(string Message);

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
/// Describes playback progress sent to the website for synchronization
/// </summary>
/// <param name="MediaId">
/// Numeric media identifier
/// </param>
/// <param name="TranslatorId">
/// Numeric translator identifier used for playback
/// </param>
/// <param name="Season">
/// Season number, or <see langword="null"/> for a movie
/// </param>
/// <param name="Episode">
/// Episode number, or <see langword="null"/> for a movie
/// </param>
/// <param name="Position">
/// Current playback position, or <see langword="null"/> when only the latest media and episode should be saved
/// </param>
/// <param name="Duration">
/// Complete stream duration, or <see langword="null"/> when only the latest media and episode should be saved
/// </param>
public sealed record PlaybackProgress(
    int MediaId,
    int TranslatorId,
    int? Season = null,
    int? Episode = null,
    TimeSpan? Position = null,
    TimeSpan? Duration = null);

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
