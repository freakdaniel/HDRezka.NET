namespace HdRezka.Tests;

public sealed class CatalogClientTests
{
    [Theory]
    [InlineData(CatalogSection.Latest, "last")]
    [InlineData(CatalogSection.Popular, "popular")]
    [InlineData(CatalogSection.Upcoming, "soon")]
    [InlineData(CatalogSection.Watching, "watching")]
    public async Task GetPageAsync_UsesSectionFilter(
        CatalogSection section,
        string expectedFilter)
    {
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
        {
            Assert.Contains($"filter={expectedFilter}", request.RequestUri!.Query);
            return Task.FromResult(StubHttpHandler.Html(CatalogHtml));
        }));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);

        var result = await client.Catalog.GetPageAsync(section);

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetPopularAsync_ParsesCardAndPagination()
    {
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
        {
            Assert.Equal("/page/2/", request.RequestUri!.AbsolutePath);
            Assert.Contains("filter=popular", request.RequestUri.Query);
            Assert.Contains("genre=2", request.RequestUri.Query);
            return Task.FromResult(StubHttpHandler.Html(CatalogHtml));
        }));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);

        var result = await client.Catalog.GetPopularAsync(
            MediaCategory.Series,
            page: 2);

        Assert.Equal(2, result.Page);
        Assert.Equal(7, result.TotalPages);
        var item = Assert.Single(result.Items);
        Assert.Equal(89179, item.Id);
        Assert.Equal("Test Series", item.Title);
        Assert.Equal(MediaCategory.Series, item.Category);
        Assert.Equal("2026, USA, Drama", item.Details);
        Assert.Equal("1 season, 8 episode", item.Information);
        Assert.Equal(new Uri("https://cdn.test/cover.jpg"), item.ImageUrl);
    }

    [Theory]
    [InlineData("new", "/new/page/2/", "last", MediaCategory.Series)]
    [InlineData("announce", "/announce/page/2/", null, MediaCategory.Series)]
    [InlineData("show", "/show/page/2/", null, MediaCategory.Show)]
    public async Task DedicatedDirectories_UseExpectedRoutes(
        string directory,
        string expectedPath,
        string? expectedFilter,
        MediaCategory expectedCategory)
    {
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
        {
            Assert.Equal(expectedPath, request.RequestUri!.AbsolutePath);
            if (expectedFilter is not null)
            {
                Assert.Contains($"filter={expectedFilter}", request.RequestUri.Query);
            }

            var html = expectedCategory == MediaCategory.Show
                ? CatalogHtml
                    .Replace("/series/", "/show/", StringComparison.Ordinal)
                    .Replace("cat series", "cat show", StringComparison.Ordinal)
                : CatalogHtml;
            return Task.FromResult(StubHttpHandler.Html(html));
        }));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);

        var result = directory switch
        {
            "new" => await client.Catalog.GetNewReleasesAsync(
                MediaCategory.Series,
                page: 2),
            "announce" => await client.Catalog.GetAnnouncementsAsync(page: 2),
            "show" => await client.Catalog.GetShowsAsync(page: 2),
            _ => throw new InvalidOperationException()
        };

        Assert.Equal(expectedCategory, Assert.Single(result.Items).Category);
    }

    [Fact]
    public async Task GetDirectoryAsync_BuildsBestGenreYearPath()
    {
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
        {
            Assert.Equal("/films/best/comedy/2025/page/3/", request.RequestUri!.AbsolutePath);
            return Task.FromResult(StubHttpHandler.Html(CatalogHtml));
        }));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);

        var result = await client.Catalog.GetDirectoryAsync(
            new CatalogQuery(MediaCategory.Film, "comedy", 2025, Best: true),
            page: 3);

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetNewestSliderAsync_UsesCompactEndpointAndParsesCards()
    {
        using var httpClient = new HttpClient(new StubHttpHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(
                "/engine/ajax/get_newest_slider_content.php",
                request.RequestUri!.AbsolutePath);
            Assert.Equal("id=2", await request.Content!.ReadAsStringAsync(cancellationToken));
            return StubHttpHandler.Html(CatalogHtml);
        }));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);

        var result = await client.Catalog.GetNewestSliderAsync(MediaCategory.Series);

        Assert.Equal("Test Series", Assert.Single(result).Title);
    }

    [Fact]
    public async Task GetQuickContentAsync_ParsesCompactMetadata()
    {
        using var httpClient = new HttpClient(new StubHttpHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/engine/ajax/quick_content.php", request.RequestUri!.AbsolutePath);
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.Contains("id=89179", form);
            Assert.Contains("is_touch=1", form);
            return StubHttpHandler.Html(QuickContentHtml);
        }));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);

        var result = await client.Catalog.GetQuickContentAsync(89179);

        Assert.Equal("Test Series", result.Title);
        Assert.Equal(MediaCategory.Series, result.Category);
        Assert.Equal(new Rating(8.93, 1771), result.Rating);
        Assert.Equal("18+", result.AgeRating);
        Assert.Equal("Drama", Assert.Single(result.Genres).Name);
        Assert.Equal("Director", Assert.Single(result.Directors).Name);
        Assert.Equal("Actor", Assert.Single(result.Cast).Name);
        Assert.Equal(6.3, Assert.Single(result.ExternalRatings).Value);
    }

    private const string CatalogHtml = """
        <!doctype html>
        <html>
        <head><title>Catalog</title></head>
        <body>
          <div class="b-content__inline_item" data-id="89179">
            <div class="b-content__inline_item-cover">
              <a href="/series/drama/89179-test.html">
                <img src="https://cdn.test/cover.jpg">
                <span class="cat series"></span>
                <span class="info">1 season, 8 episode</span>
              </a>
            </div>
            <div class="b-content__inline_item-link">
              <a href="/series/drama/89179-test.html">Test Series</a>
              <div>2026, USA, Drama</div>
            </div>
          </div>
          <div class="b-navigation">
            <a href="/page/2/">2</a>
            <a href="/page/7/">7</a>
          </div>
        </body>
        </html>
        """;

    private const string QuickContentHtml = """
        <div class="b-content__catlabel series"><i class="entity">Series</i></div>
        <div class="b-content__bubble_title">
          <a href="/series/drama/89179-test.html">Test Series</a>
        </div>
        <div class="b-content__bubble_rating">
          <span class="label">Rating:</span><b>8.93</b> (1 771)
        </div>
        <div class="b-content__bubble_text">Compact description</div>
        <div class="b-content__bubble_text">
          <span class="label">Age:</span><b>18+</b>
        </div>
        <div class="b-content__bubble_text">
          <span class="label">Genre:</span><a href="/series/drama/">Drama</a>
        </div>
        <div class="b-content__bubble_str">
          <span itemprop="director" data-id="10"><a href="/person/10-director/">Director</a></span>
        </div>
        <div class="b-content__bubble_str">
          <span itemprop="actor" data-id="11"><a href="/person/11-actor/">Actor</a></span>
        </div>
        <div class="b-content__bubble_rates">
          <span class="imdb">IMDb: <b>6.3</b><i>(150 701)</i></span>
        </div>
        """;
}
