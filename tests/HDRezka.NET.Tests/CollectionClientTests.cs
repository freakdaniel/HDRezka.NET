namespace HdRezka.Tests;

public sealed class CollectionClientTests
{
    [Fact]
    public async Task GetPageAsync_ParsesCollectionSummaries()
    {
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
        {
            Assert.Equal("/collections/page/2/", request.RequestUri!.AbsolutePath);
            return Task.FromResult(StubHttpHandler.Html(CollectionDirectoryHtml));
        }));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);

        var result = await client.Collections.GetPageAsync(page: 2);

        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.TotalPages);
        var collection = Assert.Single(result.Items);
        Assert.Equal(4547, collection.Id);
        Assert.Equal("Movies about games", collection.Title);
        Assert.Equal(73, collection.ItemCount);
        Assert.Equal(
            new Uri("https://hdrezka.test/collections/4547-movies-about-games/"),
            collection.Url);
        Assert.Equal(new Uri("https://cdn.test/collection.jpg"), collection.ImageUrl);
    }

    [Fact]
    public async Task GetAsync_ParsesCollectionContent()
    {
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
        {
            Assert.Equal(
                "/collections/4547-movies-about-games/page/2/",
                request.RequestUri!.AbsolutePath);
            return Task.FromResult(StubHttpHandler.Html(CollectionContentHtml));
        }));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);

        var result = await client.Collections.GetAsync(
            "/collections/4547-movies-about-games/",
            page: 2);

        Assert.Equal(4547, result.Id);
        Assert.Equal("Watch movies about games in HD", result.Title);
        Assert.Equal("Collection description", result.Description);
        Assert.Equal(2, result.Page);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal("Test Movie", Assert.Single(result.Items).Title);
    }

    private const string CollectionDirectoryHtml = """
        <!doctype html>
        <html>
        <head><title>Collections</title></head>
        <body>
          <div class="b-content__collections_item">
            <img class="cover" src="https://cdn.test/collection.jpg">
            <div class="num">73</div>
            <div class="title-layer">
              <a class="title" href="/collections/4547-movies-about-games/">Movies about games</a>
            </div>
          </div>
          <div class="b-navigation"><a href="/collections/page/5/">5</a></div>
        </body>
        </html>
        """;

    private const string CollectionContentHtml = """
        <!doctype html>
        <html>
        <head>
          <title>Collection</title>
          <meta property="og:description" content="Collection description">
        </head>
        <body>
          <h1>Watch movies about games in HD</h1>
          <div class="b-content__inline_item" data-id="779">
            <div class="b-content__inline_item-cover">
              <a href="/films/drama/779-test.html">
                <img src="/covers/movie.jpg">
                <span class="cat films"></span>
              </a>
            </div>
            <div class="b-content__inline_item-link">
              <a href="/films/drama/779-test.html">Test Movie</a>
              <div>1995, USA, Drama</div>
            </div>
          </div>
          <div class="b-navigation"><a href="/page/3/">3</a></div>
        </body>
        </html>
        """;
}
