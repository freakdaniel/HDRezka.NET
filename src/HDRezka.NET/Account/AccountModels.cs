namespace HdRezka;

/// <summary>
/// Identifies the gender value supported by account settings
/// </summary>
public enum AccountGender
{
    /// <summary>
    /// No gender is specified
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Male gender
    /// </summary>
    Male = 1,

    /// <summary>
    /// Female gender
    /// </summary>
    Female = 2
}

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

    /// <summary>
    /// Gets the gender selected in account settings
    /// </summary>
    public AccountGender Gender { get; init; }
}

/// <summary>
/// Describes editable general account settings
/// </summary>
/// <param name="Email">
/// Account email used by the website
/// </param>
/// <param name="Gender">
/// Gender value stored in account settings
/// </param>
public sealed record AccountSettings(string Email, AccountGender Gender);

/// <summary>
/// Describes website and player behavior saved for the account
/// </summary>
/// <param name="UpdateAddressOnSelection">
/// Whether the browser address changes with translation, season, and episode selection
/// </param>
/// <param name="AutoSwitchEpisodes">
/// Whether the player automatically starts the next episode
/// </param>
/// <param name="SelectFirstEpisode">
/// Whether the player selects the first episode by default
/// </param>
public sealed record PlaybackPreferences(
    bool UpdateAddressOnSelection,
    bool AutoSwitchEpisodes,
    bool SelectFirstEpisode);

/// <summary>
/// Identifies the current state of a Premium payment
/// </summary>
public enum PaymentStatus
{
    /// <summary>
    /// The website returned an unrecognized status
    /// </summary>
    Unknown,

    /// <summary>
    /// Payment is still being processed
    /// </summary>
    Pending,

    /// <summary>
    /// Payment completed successfully
    /// </summary>
    Successful,

    /// <summary>
    /// Payment failed or was rejected
    /// </summary>
    Failed
}

/// <summary>
/// Describes one row from Premium payment history
/// </summary>
/// <param name="Number">
/// Displayed row number
/// </param>
/// <param name="AmountLabel">
/// Amount and currency text shown by the website
/// </param>
/// <param name="Days">
/// Number of Premium days associated with the payment
/// </param>
/// <param name="Status">
/// Normalized payment status
/// </param>
/// <param name="StatusLabel">
/// Original localized status text
/// </param>
/// <param name="DateLabel">
/// Original localized payment date text
/// </param>
/// <param name="DetailsUrl">
/// Absolute payment details URL, or <see langword="null"/> when unavailable
/// </param>
public sealed record PaymentHistoryEntry(
    int Number,
    string AmountLabel,
    int Days,
    PaymentStatus Status,
    string StatusLabel,
    string DateLabel,
    Uri? DetailsUrl);

/// <summary>
/// Describes one payment method offered for Premium
/// </summary>
/// <param name="Id">
/// Identifier sent by the payment form
/// </param>
/// <param name="Name">
/// Payment method name
/// </param>
/// <param name="Description">
/// Additional method description, or an empty string when unavailable
/// </param>
/// <param name="ImageUrl">
/// Absolute method icon URL, or <see langword="null"/> when unavailable
/// </param>
public sealed record PremiumPaymentMethod(
    string Id,
    string Name,
    string Description,
    Uri? ImageUrl);

/// <summary>
/// Describes one read-only Premium plan offered for a payment method
/// </summary>
/// <param name="PaymentMethodId">
/// Payment method identifier associated with the plan
/// </param>
/// <param name="Days">
/// Subscription duration in days
/// </param>
/// <param name="Title">
/// Human-readable duration title
/// </param>
/// <param name="PriceLabel">
/// Price text shown by the website
/// </param>
/// <param name="MonthlyPriceLabel">
/// Approximate monthly price text, or <see langword="null"/> when unavailable
/// </param>
/// <param name="DiscountLabel">
/// Discount text, or <see langword="null"/> when unavailable
/// </param>
/// <param name="IsPopular">
/// Whether the website labels this plan as the most popular
/// </param>
public sealed record PremiumPlan(
    string PaymentMethodId,
    int Days,
    string Title,
    string PriceLabel,
    string? MonthlyPriceLabel,
    string? DiscountLabel,
    bool IsPopular);

/// <summary>
/// Holds read-only Premium payment methods and plans
/// </summary>
/// <param name="Methods">
/// Available payment methods
/// </param>
/// <param name="Plans">
/// Plans grouped by their payment method identifiers
/// </param>
public sealed record PremiumOffers(
    IReadOnlyList<PremiumPaymentMethod> Methods,
    IReadOnlyList<PremiumPlan> Plans);

/// <summary>
/// Identifies bookmark ordering supported by the website
/// </summary>
public enum BookmarkSort
{
    /// <summary>
    /// Newest bookmarks first
    /// </summary>
    Added,

    /// <summary>
    /// Newest release year first
    /// </summary>
    Year,

    /// <summary>
    /// Most popular media first
    /// </summary>
    Popular
}

/// <summary>
/// Describes sorting and media filtering used while loading bookmarks
/// </summary>
/// <param name="Sort">
/// Bookmark ordering
/// </param>
/// <param name="Category">
/// Media category to include, or <see cref="MediaCategory.Unknown"/> to include every category
/// </param>
public sealed record BookmarkQuery(
    BookmarkSort Sort = BookmarkSort.Added,
    MediaCategory Category = MediaCategory.Unknown);

/// <summary>
/// Describes a bookmark move accepted by the website
/// </summary>
/// <param name="Moved">
/// Number of bookmarks reported as moved
/// </param>
public sealed record BookmarkMoveResult(int Moved);

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
