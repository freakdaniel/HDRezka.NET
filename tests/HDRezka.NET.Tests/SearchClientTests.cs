namespace HdRezka.Tests;

public sealed class SearchClientTests
{
    [Fact]
    public async Task FastSearchAsync_ParsesCompactResults()
    {
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            return Task.FromResult(StubHttpHandler.Html(FastSearchHtml));
        }));
        using var search = new SearchClient("https://hdrezka.test", httpClient: httpClient);

        var results = await search.FastSearchAsync("Test");

        var result = Assert.Single(results);
        Assert.Equal("Test Film", result.Title);
        Assert.Equal(new Uri("https://hdrezka.test/films/1-test.html"), result.Url);
        Assert.Equal(7.8, result.Rating);
    }

    [Fact]
    public async Task SearchPageAsync_ParsesCategoryAndImage()
    {
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
        {
            Assert.Contains("page=2", request.RequestUri!.Query);
            return Task.FromResult(StubHttpHandler.Html(FullSearchHtml));
        }));
        using var search = new SearchClient("https://hdrezka.test", httpClient: httpClient);

        var results = await search.SearchPageAsync("Test", page: 2);

        var result = Assert.Single(results);
        Assert.Equal(MediaCategory.Anime, result.Category);
        Assert.Equal(new Uri("https://hdrezka.test/covers/1.jpg"), result.ImageUrl);
    }

    [Fact]
    public async Task SearchAllAsync_LoadsDetectedPagesConcurrentlyInPageOrder()
    {
        var active = 0;
        var maximumActive = 0;
        var bothRemainingPagesStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var httpClient = new HttpClient(new StubHttpHandler(async (request, cancellationToken) =>
        {
            var query = request.RequestUri!.Query;
            var page = query.Contains("page=1", StringComparison.Ordinal)
                ? 1
                : query.Contains("page=2", StringComparison.Ordinal)
                    ? 2
                    : 3;
            if (page > 1)
            {
                var current = Interlocked.Increment(ref active);
                maximumActive = Math.Max(maximumActive, current);
                if (current == 2)
                {
                    bothRemainingPagesStarted.TrySetResult(true);
                }

                await bothRemainingPagesStarted.Task.WaitAsync(cancellationToken);
                Interlocked.Decrement(ref active);
            }

            return StubHttpHandler.Html(
                FullSearchHtml
                    .Replace("Test Anime", $"Page {page}", StringComparison.Ordinal)
                    .Replace(
                        "</body>",
                        "<div class=\"b-navigation\"><a>3</a></div></body>",
                        StringComparison.Ordinal));
        }));
        var options = new ClientOptions { MaxConcurrentRequests = 2 };
        using var search = new SearchClient(
            "https://hdrezka.test",
            options,
            httpClient);

        var results = await search.SearchAllAsync("Test");

        Assert.Equal(["Page 1", "Page 2", "Page 3"], results.Select(item => item.Title));
        Assert.Equal(2, maximumActive);
    }

    private const string FastSearchHtml = """
        <ul class="b-search__section_list">
          <li>
            <a href="/films/1-test.html"><span class="enty">Test Film</span></a>
            <span class="rating">7.8</span>
          </li>
        </ul>
        """;

    private const string FullSearchHtml = """
        <!doctype html>
        <html>
        <head><title>Search</title></head>
        <body>
          <div class="b-content__inline_item">
            <div class="b-content__inline_item-link"><a href="/animation/1-test.html">Test Anime</a></div>
            <div class="b-content__inline_item-cover"><img src="/covers/1.jpg"></div>
            <span class="cat animation">Anime</span>
          </div>
        </body>
        </html>
        """;
}
