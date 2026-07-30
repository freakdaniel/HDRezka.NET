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
