using System.Text.Json;

namespace HdRezka.Tests;

public sealed class CommentClientTests
{
    [Fact]
    public async Task GetPageAsync_UsesAjaxEndpointAndParsesNestedComments()
    {
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith(".html", StringComparison.Ordinal))
            {
                return Task.FromResult(StubHttpHandler.Html(MediaHtml));
            }

            Assert.Equal("/ajax/get_comments/", request.RequestUri!.AbsolutePath);
            Assert.Contains("news_id=123", request.RequestUri.Query);
            Assert.Contains("cstart=2", request.RequestUri.Query);
            return Task.FromResult(
                StubHttpHandler.Json(
                    JsonSerializer.Serialize(
                        new
                        {
                            comments = CommentsHtml,
                            navigation = NavigationHtml,
                            last_update_id = 9001
                        })));
        }));
        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/drama/123-test.html",
            httpClient: httpClient);

        var page = await media.Comments.GetPageAsync(page: 2);

        Assert.Equal(2, page.Page);
        Assert.Equal(4, page.TotalPages);
        Assert.Equal(9001, page.LastUpdateId);
        Assert.Equal(2, page.Items.Count);
        var root = page.Items[0];
        Assert.Equal(101, root.Id);
        Assert.Null(root.ParentId);
        Assert.Equal("First author", root.Author);
        Assert.Equal("Root text", root.Text);
        Assert.Equal(3, root.Likes);
        Assert.Equal(77, root.AuthorId);
        Assert.Equal(new Uri("https://hdrezka.test/user/77/"), root.AuthorUrl);
        Assert.Contains("<b>Root</b>", root.Html);
        Assert.True(root.IsLikedByCurrentAccount);
        Assert.True(root.CanDelete);
        Assert.True(root.CanReport);
        Assert.Equal(new Uri("https://cdn.test/avatar.jpg"), root.AvatarUrl);
        Assert.Equal(
            new Uri("https://hdrezka.test/films/drama/123-test.html#comment101"),
            root.Url);
        Assert.Equal(101, page.Items[1].ParentId);
        Assert.Equal(1, page.Items[1].Depth);
    }

    [Fact]
    public async Task AddAsync_SubmitsAuthenticatedCommentFields()
    {
        var requestNumber = 0;
        using var httpClient = new HttpClient(new StubHttpHandler(async (request, _) =>
        {
            requestNumber++;
            if (requestNumber == 1)
            {
                return StubHttpHandler.Html(MediaHtml);
            }

            Assert.Equal("/ajax/add_comment/", request.RequestUri!.AbsolutePath);
            Assert.Equal(
                "https://hdrezka.test/films/drama/123-test.html",
                request.Headers.Referrer!.AbsoluteUri);
            var form = await request.Content!.ReadAsStringAsync();
            Assert.Contains("comments=A+useful+review", form);
            Assert.Contains("post_id=123", form);
            Assert.Contains("parent=0", form);
            Assert.Contains("replyto_id=0", form);
            return StubHttpHandler.Json(
                """
                {
                  "success": true,
                  "on_moderation": false,
                  "comment_id": 701,
                  "message": "<div>Comment was published</div>"
                }
                """);
        }));
        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/drama/123-test.html",
            httpClient: httpClient);

        var result = await media.Comments.AddAsync("A useful review");

        Assert.Equal(701, result.Id);
        Assert.Null(result.ParentId);
        Assert.False(result.IsPendingModeration);
        Assert.Equal("Comment was published", result.Message);
    }

    [Fact]
    public async Task ReplyAsync_SubmitsParentIdentifiers()
    {
        var requestNumber = 0;
        using var httpClient = new HttpClient(new StubHttpHandler(async (request, _) =>
        {
            requestNumber++;
            if (requestNumber == 1)
            {
                return StubHttpHandler.Html(MediaHtml);
            }

            var form = await request.Content!.ReadAsStringAsync();
            Assert.Contains("parent=101", form);
            Assert.Contains("replyto_id=101", form);
            return StubHttpHandler.Json(
                """
                {
                  "success": true,
                  "on_moderation": true,
                  "comment_id": 702,
                  "message": "Reply is waiting for moderation"
                }
                """);
        }));
        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/drama/123-test.html",
            httpClient: httpClient);

        var result = await media.Comments.ReplyAsync(101, "A detailed reply");

        Assert.Equal(702, result.Id);
        Assert.Equal(101, result.ParentId);
        Assert.True(result.IsPendingModeration);
    }

    [Fact]
    public async Task AddAsync_ThrowsEveryWebsiteValidationMessage()
    {
        var requestNumber = 0;
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
        {
            requestNumber++;
            return Task.FromResult(
                requestNumber == 1
                    ? StubHttpHandler.Html(MediaHtml)
                    : StubHttpHandler.Json(
                        """
                        {
                          "success": false,
                          "on_moderation": false,
                          "comment_id": 0,
                          "message": ["Comment is too short", "Try again later"]
                        }
                        """));
        }));
        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/drama/123-test.html",
            httpClient: httpClient);

        var exception = await Assert.ThrowsAsync<CommentOperationException>(
            () => media.Comments.AddAsync("Short but not empty"));

        Assert.Equal("Comment is too short Try again later", exception.Message);
    }

    [Fact]
    public async Task DeleteAsync_LoadsSecurityTokenAndDeletesOwnedComment()
    {
        var requestNumber = 0;
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
        {
            requestNumber++;
            if (requestNumber == 1)
            {
                return Task.FromResult(StubHttpHandler.Html(MediaHtml));
            }

            if (requestNumber == 2)
            {
                Assert.Equal("/settings/", request.RequestUri!.AbsolutePath);
                return Task.FromResult(StubHttpHandler.Html(SettingsFormHtml));
            }

            Assert.Equal("/engine/ajax/deletecomments.php", request.RequestUri!.AbsolutePath);
            Assert.Contains("id=701", request.RequestUri.Query);
            Assert.Contains("dle_allow_hash=delete-token", request.RequestUri.Query);
            Assert.Contains("type=0", request.RequestUri.Query);
            Assert.Contains("area=ajax", request.RequestUri.Query);
            return Task.FromResult(
                StubHttpHandler.Json("""{"success":true,"message":""}"""));
        }));
        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/drama/123-test.html",
            httpClient: httpClient);

        await media.Comments.DeleteAsync(701);

        Assert.Equal(3, requestNumber);
    }

    [Fact]
    public async Task CommentLikes_ToggleAndLoadUsersThroughDirectEndpoints()
    {
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith(".html", StringComparison.Ordinal))
            {
                return Task.FromResult(StubHttpHandler.Html(MediaHtml));
            }

            if (request.RequestUri.AbsolutePath.Contains("comments_like.php", StringComparison.Ordinal))
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Contains("id=101", request.RequestUri.Query);
                return Task.FromResult(
                    StubHttpHandler.Json("""{"success":true,"count":4,"type":"plus"}"""));
            }

            Assert.Equal("/ajax/comments_likes/", request.RequestUri.AbsolutePath);
            return Task.FromResult(
                StubHttpHandler.Json(
                    """
                    {"success":true,"message":"<a href='/user/77/' title='First author'><img src='/avatar.jpg'></a>"}
                    """));
        }));
        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/drama/123-test.html",
            httpClient: httpClient);

        var result = await media.Comments.ToggleLikeAsync(101);
        var users = await media.Comments.GetLikeUsersAsync(101);

        Assert.True(result.IsLiked);
        Assert.Equal(4, result.Count);
        var user = Assert.Single(users);
        Assert.Equal("First author", user.Name);
        Assert.Equal(new Uri("https://hdrezka.test/user/77/"), user.ProfileUrl);
    }

    [Fact]
    public async Task ReportAsync_SubmitsCommentComplaintFields()
    {
        var requestNumber = 0;
        using var httpClient = new HttpClient(new StubHttpHandler(async (request, cancellationToken) =>
        {
            requestNumber++;
            if (requestNumber == 1)
            {
                return StubHttpHandler.Html(MediaHtml);
            }

            Assert.Equal("/engine/ajax/complaint.php", request.RequestUri!.AbsolutePath);
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.Contains("id=101", form);
            Assert.Contains("issue_id=2", form);
            Assert.Contains("text=Spam+links", form);
            Assert.Contains("action=comments", form);
            return StubHttpHandler.Json("""{"success":true}""");
        }));
        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/drama/123-test.html",
            httpClient: httpClient);

        await media.Comments.ReportAsync(101, 2, "Spam links");
    }

    private const string CommentsHtml = """
        <ol class="comments-tree-list">
          <li class="comments-tree-item" data-id="101" data-indent="0">
            <div class="b-comment">
              <div class="ava"><img src="https://cdn.test/avatar.jpg"></div>
              <div class="message">
                <div class="info">
                  <span class="name"><a href="/user/77/">First author</a></span>
                  <span class="date">today</span>
                </div>
                <div class="text"><div id="comm-id-101"><b>Root</b> text</div></div>
                <span class="b-comment__likes_count">(<i>3</i>)</span>
                <button class="b-comment__like_it disabled"></button>
                <button class="b-comment__report"></button>
                <a onclick="sof.comments.deleteComment(101)">delete</a>
              </div>
            </div>
            <ol class="comments-tree-list">
              <li class="comments-tree-item" data-id="102" data-indent="1">
                <div class="b-comment">
                  <span class="name">Second author</span>
                  <span class="date">yesterday</span>
                  <div class="text"><div id="comm-id-102">Reply text</div></div>
                </div>
              </li>
            </ol>
          </li>
        </ol>
        """;

    private const string MediaHtml = """
        <html>
        <head>
          <meta property="og:type" content="video.movie">
          <meta property="og:image" content="/cover.jpg">
        </head>
        <body>
          <input id="post_id" value="123">
          <h1 class="b-post__title">Commented movie</h1>
          <ul id="translators-list"><li data-translator_id="56">Dub</li></ul>
        </body>
        </html>
        """;

    private const string SettingsFormHtml = """
        <html>
        <head><title>Test User</title></head>
        <body>
          <form id="userinfo" action="/user/1273253/">
            <input name="username_id" value="1273253">
            <input name="dle_allow_hash" value="delete-token">
          </form>
        </body>
        </html>
        """;

    private const string NavigationHtml = """
        <div class="b-navigation">
          <a>1</a>
          <span>2</span>
          <a>4</a>
        </div>
        """;
}
