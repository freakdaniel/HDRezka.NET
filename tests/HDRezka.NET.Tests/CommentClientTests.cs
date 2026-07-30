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
        Assert.Equal(new Uri("https://cdn.test/avatar.jpg"), root.AvatarUrl);
        Assert.Equal(
            new Uri("https://hdrezka.test/films/drama/123-test.html#comment101"),
            root.Url);
        Assert.Equal(101, page.Items[1].ParentId);
        Assert.Equal(1, page.Items[1].Depth);
    }

    private const string CommentsHtml = """
        <ol class="comments-tree-list">
          <li class="comments-tree-item" data-id="101" data-indent="0">
            <div class="b-comment">
              <div class="ava"><img src="https://cdn.test/avatar.jpg"></div>
              <div class="message">
                <div class="info">
                  <span class="name">First author</span>
                  <span class="date">today</span>
                </div>
                <div class="text"><div id="comm-id-101">Root text</div></div>
                <span class="b-comment__likes_count">(<i>3</i>)</span>
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

    private const string NavigationHtml = """
        <div class="b-navigation">
          <a>1</a>
          <span>2</span>
          <a>4</a>
        </div>
        """;
}
