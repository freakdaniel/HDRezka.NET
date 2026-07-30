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
        return new Comment(
            id,
            FindParentId(item),
            depth,
            author,
            avatarUrl,
            item.QuerySelector(".date")?.TextContent.Trim() ?? "",
            text,
            likes,
            new UriBuilder(mediaUrl) { Fragment = $"comment{id}" }.Uri);
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
}
