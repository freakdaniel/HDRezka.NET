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
    Uri Url);

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
