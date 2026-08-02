namespace HdRezka;

/// <summary>
/// Describes one user comment from a media page
/// </summary>
/// <param name="Id">
/// Numeric comment identifier used by the website
/// </param>
/// <param name="ParentId">
/// Parent comment identifier, or <see langword="null"/> for a root comment
/// </param>
/// <param name="Depth">
/// Reply nesting depth reported by the website
/// </param>
/// <param name="Author">
/// Author display name
/// </param>
/// <param name="AvatarUrl">
/// Absolute avatar URL, or <see langword="null"/> when no avatar is available
/// </param>
/// <param name="DateLabel">
/// Date and time text shown by the website
/// </param>
/// <param name="Text">
/// Comment text without surrounding interface controls
/// </param>
/// <param name="Likes">
/// Current like count
/// </param>
/// <param name="Url">
/// Absolute media page URL with the comment fragment
/// </param>
public sealed record Comment(
    long Id,
    long? ParentId,
    int Depth,
    string Author,
    Uri? AvatarUrl,
    string DateLabel,
    string Text,
    int Likes,
    Uri Url)
{
    /// <summary>
    /// Gets the numeric author identifier
    /// </summary>
    /// <value>
    /// Author identifier, or <see langword="null"/> when the comment does not link a profile
    /// </value>
    public int? AuthorId { get; init; }

    /// <summary>
    /// Gets the absolute author profile URL
    /// </summary>
    /// <value>
    /// Profile URL, or <see langword="null"/> for deleted or unavailable accounts
    /// </value>
    public Uri? AuthorUrl { get; init; }

    /// <summary>
    /// Gets the original formatted comment markup without surrounding controls
    /// </summary>
    public string Html { get; init; } = "";

    /// <summary>
    /// Gets whether the current authenticated account has liked this comment
    /// </summary>
    public bool IsLikedByCurrentAccount { get; init; }

    /// <summary>
    /// Gets whether the loaded markup exposes deletion for the current account
    /// </summary>
    public bool CanDelete { get; init; }

    /// <summary>
    /// Gets whether the loaded markup exposes a complaint action
    /// </summary>
    public bool CanReport { get; init; }
}

/// <summary>
/// Describes the result of toggling a comment like
/// </summary>
/// <param name="IsLiked">
/// Whether the current account likes the comment after the operation
/// </param>
/// <param name="Count">
/// Updated total like count
/// </param>
public sealed record CommentLikeResult(bool IsLiked, int Count);

/// <summary>
/// Describes one account returned by the comment likes popup
/// </summary>
/// <param name="Name">
/// Display name shown by the website
/// </param>
/// <param name="ProfileUrl">
/// Absolute account profile URL, or <see langword="null"/> when unavailable
/// </param>
/// <param name="AvatarUrl">
/// Absolute avatar URL, or <see langword="null"/> when unavailable
/// </param>
public sealed record CommentLikeUser(string Name, Uri? ProfileUrl, Uri? AvatarUrl);

/// <summary>
/// Holds one comment page together with pagination and update information
/// </summary>
/// <param name="Items">
/// Comments in the same order and nesting sequence as the website
/// </param>
/// <param name="Page">
/// One-based page number
/// </param>
/// <param name="TotalPages">
/// Total comment page count detected from website navigation
/// </param>
/// <param name="LastUpdateId">
/// Latest update identifier returned by the website
/// </param>
public sealed record CommentPage(
    IReadOnlyList<Comment> Items,
    int Page,
    int TotalPages,
    long? LastUpdateId);

/// <summary>
/// Describes a comment or reply accepted by the website
/// </summary>
/// <param name="Id">
/// Numeric identifier assigned to the created comment
/// </param>
/// <param name="ParentId">
/// Parent comment identifier, or <see langword="null"/> for a root comment
/// </param>
/// <param name="IsPendingModeration">
/// <see langword="true"/> when the website accepted the comment for moderation instead of publishing it immediately
/// </param>
/// <param name="Message">
/// Confirmation text returned by the website with markup removed
/// </param>
public sealed record CommentSubmission(
    long Id,
    long? ParentId,
    bool IsPendingModeration,
    string Message);
