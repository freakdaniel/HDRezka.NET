namespace HdRezka.Tests;

public sealed class AccountClientTests
{
    [Fact]
    public async Task GetProfileAsync_ParsesAccountMetadata()
    {
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
        {
            Assert.Equal("/settings/", request.RequestUri!.AbsolutePath);
            return Task.FromResult(StubHttpHandler.Html(ProfileHtml));
        }));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);

        var profile = await client.Account.GetProfileAsync();

        Assert.Equal(1273253, profile.Id);
        Assert.Equal("Test User", profile.Username);
        Assert.Equal("test@example.com", profile.Email);
        Assert.Equal(new Uri("https://cdn.test/avatar.jpg"), profile.AvatarUrl);
        Assert.Equal(AccountTier.Standard, profile.Tier);
        Assert.False(profile.IsPremium);
        Assert.Equal(12, profile.ContinueWatchingCount);
        Assert.Equal(new Uri("https://hdrezka.test/user/1273253/"), profile.ProfileUrl);
    }

    [Fact]
    public async Task GetContinueWatchingAsync_ParsesSeriesAndMovieEntries()
    {
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
        {
            Assert.Equal("/continue/", request.RequestUri!.AbsolutePath);
            return Task.FromResult(StubHttpHandler.Html(ContinueWatchingHtml));
        }));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);

        var entries = await client.Account.GetContinueWatchingAsync();

        Assert.Equal(2, entries.Count);
        var series = entries[0];
        Assert.Equal(101, series.Id);
        Assert.Equal("Test Series", series.Title);
        Assert.Equal(MediaCategory.Series, series.Category);
        Assert.Equal(new DateOnly(2026, 7, 30), series.Date);
        Assert.Equal(2, series.Season);
        Assert.Equal(5, series.Episode);
        Assert.Equal("Dub", series.Translator);
        Assert.Equal(3, series.RemainingEpisodes);
        Assert.True(series.IsWatched);

        var movie = entries[1];
        Assert.Null(movie.Date);
        Assert.Null(movie.Season);
        Assert.Null(movie.Episode);
        Assert.Equal("Voice-over", movie.Translator);
        Assert.False(movie.IsWatched);
    }

    [Fact]
    public async Task GetBookmarksAsync_LoadsEveryFolder()
    {
        var requestedPaths = new List<string>();
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
        {
            requestedPaths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(
                StubHttpHandler.Html(
                    request.RequestUri.AbsolutePath == "/favorites/"
                        ? BookmarksRootHtml
                        : SecondBookmarkFolderHtml));
        }));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);

        var folders = await client.Account.GetBookmarksAsync();

        Assert.Equal(2, folders.Count);
        Assert.Equal(["/favorites/", "/favorites/20/"], requestedPaths);
        Assert.Equal("Watching", folders[0].Name);
        Assert.Equal("First Movie", Assert.Single(folders[0].Items).Title);
        Assert.Equal("Later", folders[1].Name);
        Assert.Equal("Second Series", Assert.Single(folders[1].Items).Title);
    }

    private const string ProfileHtml = """
        <!doctype html>
        <html>
        <head><title>Test User</title></head>
        <body>
          <input id="member_user_id" value="1273253">
          <input id="ctrl_token_id" value="eyJhbGciOiJub25lIn0.eyJkYXRhIjp7Im1lbWJlcl9pZCI6eyJpc19wcmVtaXVtIjoiMCJ9fX0.signature">
          <span id="saves-count">12</span>
          <input id="email" value="test@example.com">
          <div id="avatar-profile"><img src="https://cdn.test/avatar.jpg"></div>
        </body>
        </html>
        """;

    private const string ContinueWatchingHtml = """
        <!doctype html>
        <html>
        <head><title>Continue</title></head>
        <body>
          <div id="videosave-101" class="b-videosaves__list_item watched-row">
            <div class="td date">30-07-2026</div>
            <div class="td title">
              <a href="/series/drama/501-test-series.html" data-cover_url="/covers/series.jpg">Test Series</a>
              <small>(2026 - ...)</small>
            </div>
            <div class="td info">
              2 сезон 5 серия (Dub)
              <span class="info-holder">
                <a class="new-episode"><b>3</b> серии</a>
              </span>
            </div>
          </div>
          <div id="videosave-102" class="b-videosaves__list_item">
            <div class="td date">вчера</div>
            <div class="td title">
              <a href="/films/comedy/502-test-movie.html" data-cover_url="/covers/movie.jpg">Test Movie</a>
              <small>(2025)</small>
            </div>
            <div class="td info">Voice-over</div>
          </div>
        </body>
        </html>
        """;

    private const string BookmarksRootHtml = """
        <!doctype html>
        <html>
        <head><title>Bookmarks</title></head>
        <body>
          <div class="b-favorites_content__cats_list_item" data-cat_id="10">
            <a class="b-favorites_content__cats_list_link active" href="/favorites/10/">
              <span class="name">Watching</span>
              <span class="num-holder"><b>1</b></span>
            </a>
          </div>
          <div class="b-favorites_content__cats_list_item" data-cat_id="20">
            <a class="b-favorites_content__cats_list_link" href="/favorites/20/">
              <span class="name">Later</span>
              <span class="num-holder"><b>1</b></span>
            </a>
          </div>
          <div class="b-content__inline_item" data-id="601">
            <div class="b-content__inline_item-cover">
              <a href="/films/drama/601-first.html"><img src="/covers/first.jpg"></a>
              <span class="cat films"></span>
            </div>
            <div class="b-content__inline_item-link">
              <a href="/films/drama/601-first.html">First Movie</a>
              <div>2025, USA, Drama</div>
            </div>
          </div>
        </body>
        </html>
        """;

    private const string SecondBookmarkFolderHtml = """
        <!doctype html>
        <html>
        <head><title>Bookmarks</title></head>
        <body>
          <div class="b-content__inline_item" data-id="602">
            <div class="b-content__inline_item-cover">
              <a href="/series/drama/602-second.html"><img src="/covers/second.jpg"></a>
              <span class="cat series"></span>
              <span class="info">1 season</span>
            </div>
            <div class="b-content__inline_item-link">
              <a href="/series/drama/602-second.html">Second Series</a>
              <div>2026, USA, Drama</div>
            </div>
          </div>
        </body>
        </html>
        """;
}
