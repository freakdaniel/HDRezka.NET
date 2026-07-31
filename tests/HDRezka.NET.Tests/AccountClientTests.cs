namespace HdRezka.Tests;

public sealed class AccountClientTests
{
    [Fact]
    public async Task SavePlaybackProgressAsync_SendsWebsitePlaybackFields()
    {
        using var httpClient = new HttpClient(new StubHttpHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/ajax/send_save/", request.RequestUri!.AbsolutePath);
            Assert.Contains("t=", request.RequestUri.Query);
            var form = await ParseFormAsync(request, cancellationToken);
            Assert.Equal("66689", form["post_id"]);
            Assert.Equal("56", form["translator_id"]);
            Assert.Equal("1", form["season"]);
            Assert.Equal("4", form["episode"]);
            Assert.Equal("83.25", form["current_time"]);
            Assert.Equal("3600", form["duration"]);
            return StubHttpHandler.Json("""{"success":1}""");
        }));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);

        await client.Account.SavePlaybackProgressAsync(
            new PlaybackProgress(
                66689,
                56,
                Season: 1,
                Episode: 4,
                Position: TimeSpan.FromSeconds(83.25),
                Duration: TimeSpan.FromHours(1)));
    }

    [Fact]
    public async Task SetContinueWatchingWatchedAsync_TogglesOnlyWhenStateChanges()
    {
        var requests = 0;
        using var httpClient = new HttpClient(new StubHttpHandler(async (request, cancellationToken) =>
        {
            requests++;
            Assert.Equal("/engine/ajax/cdn_saves_view.php", request.RequestUri!.AbsolutePath);
            var form = await ParseFormAsync(request, cancellationToken);
            Assert.Equal("101", form["id"]);
            return StubHttpHandler.Json("""{"success":true}""");
        }));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);
        var entry = CreateContinueWatchingEntry(isWatched: false);

        var unchanged = await client.Account.SetContinueWatchingWatchedAsync(entry, false);
        var watched = await client.Account.SetContinueWatchingWatchedAsync(entry, true);

        Assert.Same(entry, unchanged);
        Assert.True(watched.IsWatched);
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task RemoveContinueWatchingAsync_SendsSavedPositionId()
    {
        using var httpClient = new HttpClient(new StubHttpHandler(async (request, cancellationToken) =>
        {
            Assert.Equal("/engine/ajax/cdn_saves_remove.php", request.RequestUri!.AbsolutePath);
            var form = await ParseFormAsync(request, cancellationToken);
            Assert.Equal("101", form["id"]);
            return StubHttpHandler.Json("""{"success":"1"}""");
        }));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);

        await client.Account.RemoveContinueWatchingAsync(101);
    }

    [Fact]
    public async Task BookmarkMutations_SendExpectedActions()
    {
        var requests = new List<IReadOnlyDictionary<string, string>>();
        using var httpClient = new HttpClient(new StubHttpHandler(async (request, cancellationToken) =>
        {
            Assert.Equal("/ajax/favorites/", request.RequestUri!.AbsolutePath);
            var form = await ParseFormAsync(request, cancellationToken);
            requests.Add(form);
            return form["action"] switch
            {
                "add_cat" => StubHttpHandler.Json(
                    """{"success":true,"id":"42","name":"Watch later"}"""),
                "add_post" => StubHttpHandler.Json("""{"success":true}"""),
                "remove_cat" => StubHttpHandler.Json("""{"success":true}"""),
                _ => throw new InvalidOperationException()
            };
        }));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);

        var folder = await client.Account.CreateBookmarkFolderAsync("  Watch later  ");
        await client.Account.ToggleBookmarkAsync(66689, folder.Id);
        await client.Account.DeleteBookmarkFolderAsync(folder.Id);

        Assert.Equal(42, folder.Id);
        Assert.Equal("Watch later", folder.Name);
        Assert.Empty(folder.Items);
        Assert.Equal(new Uri("https://hdrezka.test/favorites/42/"), folder.Url);
        Assert.Equal(
            ["add_cat", "add_post", "remove_cat"],
            requests.Select(form => form["action"]));
        Assert.Equal("Watch later", requests[0]["name"]);
        Assert.Equal("66689", requests[1]["post_id"]);
        Assert.Equal("42", requests[1]["cat_id"]);
        Assert.Equal("42", requests[2]["cat_id"]);
    }

    [Fact]
    public async Task AccountMutation_WhenRejected_ThrowsReadableException()
    {
        using var httpClient = new HttpClient(new StubHttpHandler((_, _) =>
            Task.FromResult(
                StubHttpHandler.Json(
                    """{"success":false,"message":"Authentication required"}"""))));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);

        var exception = await Assert.ThrowsAsync<AccountOperationException>(
            () => client.Account.RemoveContinueWatchingAsync(101));

        Assert.Equal("Authentication required", exception.Message);
    }

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

    private static ContinueWatchingEntry CreateContinueWatchingEntry(bool isWatched) =>
        new(
            101,
            "Test Series",
            new Uri("https://hdrezka.test/series/drama/501-test-series.html"),
            new Uri("https://hdrezka.test/covers/series.jpg"),
            MediaCategory.Series,
            "today",
            null,
            "2026",
            "1 season 1 episode",
            1,
            1,
            "Dub",
            isWatched,
            3);

    private static async Task<IReadOnlyDictionary<string, string>> ParseFormAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var content = await request.Content!.ReadAsStringAsync(cancellationToken);
        return content
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')),
                pair => Uri.UnescapeDataString(pair[1].Replace('+', ' ')),
                StringComparer.Ordinal);
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
