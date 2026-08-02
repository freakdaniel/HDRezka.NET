using System.Globalization;
using AngleSharp.Dom;

namespace HdRezka.Scraping;

internal static class CommentParser
{
    public static async Task<CommentPage> ParseAsync(
        string? commentsHtml,
        string? navigationHtml,
        Uri origin,
        Uri mediaUrl,
        int page,
        long? lastUpdateId,
        CancellationToken cancellationToken)
    {
        if (commentsHtml is null || navigationHtml is null)
        {
            throw new ParseException("The comments response has no HTML fragments.");
        }

        var commentsDocument = await Parsing.ParseDocumentAsync(
            commentsHtml,
            cancellationToken).ConfigureAwait(false);
        var navigationDocument = await Parsing.ParseDocumentAsync(
            navigationHtml,
            cancellationToken).ConfigureAwait(false);
        var items = commentsDocument
            .QuerySelectorAll(".comments-tree-item")
            .Select(item => ParseComment(item, origin, mediaUrl))
            .ToList();
        return new CommentPage(
            items,
            page,
            CatalogParser.ParseTotalPages(navigationDocument),
            lastUpdateId);
    }

    public static async Task<string> ParseMessageAsync(
        IEnumerable<string> fragments,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(
            string.Join(" ", fragments),
            cancellationToken).ConfigureAwait(false);
        return string.Join(
            " ",
            (document.Body?.TextContent ?? "")
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static Comment ParseComment(IElement item, Uri origin, Uri mediaUrl)
    {
        var id = ParseRequiredLong(item.GetAttribute("data-id"), "comment ID");
        var author = item.QuerySelector(".name")?.TextContent.Trim();
        var text = item.QuerySelector($"#comm-id-{id}")?.TextContent.Trim() ??
            item.QuerySelector(".text")?.TextContent.Trim();
        if (string.IsNullOrWhiteSpace(author) || string.IsNullOrWhiteSpace(text))
        {
            throw new ParseException("A comment has no author or text.");
        }

        var depth = int.TryParse(
            item.GetAttribute("data-indent"),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedDepth)
                ? parsedDepth
                : 0;
        var likes = int.TryParse(
            item.QuerySelector(".b-comment__likes_count i")?.TextContent.Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedLikes)
                ? parsedLikes
                : 0;
        var avatarValue = item.QuerySelector(".ava img")?.GetAttribute("src");
        var avatarUrl = string.IsNullOrWhiteSpace(avatarValue)
            ? null
            : new Uri(origin, avatarValue);
        var textElement = item.QuerySelector($"#comm-id-{id}") ?? item.QuerySelector(".text");
        var authorLink = item.QuerySelector(".name a, a.name");
        var authorUrl = string.IsNullOrWhiteSpace(authorLink?.GetAttribute("href"))
            ? null
            : new Uri(origin, authorLink.GetAttribute("href"));
        return new Comment(
            id,
            FindParentId(item),
            depth,
            author,
            avatarUrl,
            item.QuerySelector(".date")?.TextContent.Trim() ?? "",
            text,
            likes,
            new UriBuilder(mediaUrl) { Fragment = $"comment{id}" }.Uri)
        {
            AuthorId = ParseAuthorId(authorUrl),
            AuthorUrl = authorUrl,
            Html = textElement?.InnerHtml.Trim() ?? "",
            IsLikedByCurrentAccount = item.QuerySelector(".b-comment__like_it.disabled") is not null,
            CanDelete = item.QuerySelector("[onclick*=\"deleteComment\"], .delete-comment") is not null,
            CanReport = item.QuerySelector(".b-comment__report") is not null
        };
    }

    public static async Task<IReadOnlyList<CommentLikeUser>> ParseLikeUsersAsync(
        string html,
        Uri origin,
        CancellationToken cancellationToken)
    {
        var document = await Parsing.ParseDocumentAsync(html, cancellationToken)
            .ConfigureAwait(false);
        return document.QuerySelectorAll("a")
            .Select(link =>
            {
                var name = link.GetAttribute("title")?.Trim() ?? link.TextContent.Trim();
                var href = link.GetAttribute("href");
                var image = link.QuerySelector("img")?.GetAttribute("src");
                return new CommentLikeUser(
                    name,
                    string.IsNullOrWhiteSpace(href) ? null : new Uri(origin, href),
                    string.IsNullOrWhiteSpace(image) ? null : new Uri(origin, image));
            })
            .Where(user => !string.IsNullOrWhiteSpace(user.Name))
            .ToList();
    }

    private static long? FindParentId(IElement item)
    {
        var parent = item.ParentElement;
        while (parent is not null)
        {
            if (parent.ClassList.Contains("comments-tree-item"))
            {
                return long.TryParse(
                    parent.GetAttribute("data-id"),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var id)
                        ? id
                        : null;
            }

            parent = parent.ParentElement;
        }

        return null;
    }

    private static long ParseRequiredLong(string? value, string description) =>
        long.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var result)
                ? result
                : throw new ParseException($"Could not parse {description}.");

    private static int? ParseAuthorId(Uri? url)
    {
        if (url is null)
        {
            return null;
        }

        var segments = url.AbsolutePath.Trim('/').Split('/');
        return segments.Length >= 2 &&
            segments[0].Equals("user", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(segments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                ? id
                : null;
    }
}
