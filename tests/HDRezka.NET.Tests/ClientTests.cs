namespace HdRezka.Tests;

public sealed class ClientTests
{
    [Fact]
    public async Task GetAsync_RewritesAbsoluteUrlToConfiguredOrigin()
    {
        Uri? requestedUri = null;
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
        {
            requestedUri = request.RequestUri;
            return Task.FromResult(StubHttpHandler.Html(PageHtml));
        }));
        using var client = new Client("https://mirror.test", httpClient: httpClient);

        _ = await client.GetAsync("https://original.test/films/42-title.html");

        Assert.Equal(new Uri("https://mirror.test/films/42-title.html"), requestedUri);
    }

    [Fact]
    public async Task GetAsync_SharesActiveLoadWithoutSharingCallerCancellation()
    {
        var requests = 0;
        var requestStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRequest = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var httpClient = new HttpClient(new StubHttpHandler(async (_, cancellationToken) =>
        {
            Interlocked.Increment(ref requests);
            requestStarted.TrySetResult(true);
            await releaseRequest.Task.WaitAsync(cancellationToken);
            return StubHttpHandler.Html(PageHtml);
        }));
        using var client = new Client("https://mirror.test", httpClient: httpClient);

        var survivingLoad = client.GetAsync("/films/42-title.html");
        await requestStarted.Task;
        using var cancellation = new CancellationTokenSource();
        var canceledLoad = client.GetAsync("/films/42-title.html", cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledLoad);
        releaseRequest.TrySetResult(true);
        using var media = await survivingLoad;

        Assert.Equal(1, requests);
        Assert.Equal("Title", media.Name);
    }

    [Fact]
    public async Task GetAsync_RetainsResponsesUntilConfiguredExpiration()
    {
        var requests = 0;
        var timeProvider = new ManualTimeProvider();
        using var httpClient = new HttpClient(new StubHttpHandler((_, _) =>
        {
            requests++;
            return Task.FromResult(StubHttpHandler.Html(PageHtml));
        }));
        var options = new ClientOptions
        {
            ResponseCacheDuration = TimeSpan.FromMinutes(1),
            TimeProvider = timeProvider
        };
        using var client = new Client("https://mirror.test", options, httpClient);

        using var first = await client.GetAsync("/films/42-title.html");
        using var second = await client.GetAsync("/films/42-title.html");
        timeProvider.Advance(TimeSpan.FromMinutes(2));
        using var expired = await client.GetAsync("/films/42-title.html");

        Assert.Equal(2, requests);
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task GetAsync_DoesNotReuseCachedResponseAfterCookieChange()
    {
        var requests = 0;
        using var httpClient = new HttpClient(new StubHttpHandler((_, _) =>
        {
            requests++;
            return Task.FromResult(StubHttpHandler.Html(PageHtml));
        }));
        var options = new ClientOptions { ResponseCacheDuration = TimeSpan.FromMinutes(1) };
        using var client = new Client("https://mirror.test", options, httpClient);

        using var first = await client.GetAsync("/films/42-title.html");
        client.Options.Cookies["dle_user_id"] = "different-account";
        using var second = await client.GetAsync("/films/42-title.html");

        Assert.Equal(2, requests);
    }

    [Fact]
    public async Task LoginAsync_CapturesCookiesAndVerifiesAuthenticatedPage()
    {
        using var httpClient = new HttpClient(new StubHttpHandler(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal(new Uri("https://mirror.test/"), request.Headers.Referrer);
                var form = await request.Content!.ReadAsStringAsync(cancellationToken);
                Assert.Contains("login_name=mail%40example.com", form);
                Assert.Contains("login_password=secret", form);
                Assert.Contains("login_not_save=0", form);

                var response = StubHttpHandler.Json("""{"success":true}""");
                response.Headers.TryAddWithoutValidation(
                    "Set-Cookie",
                    [
                        "dle_user_id=deleted; Max-Age=0; Path=/; HttpOnly",
                        "dle_password=deleted; Max-Age=0; Path=/; HttpOnly",
                        "PHPSESSID=session-value; Path=/; HttpOnly",
                        "dle_user_id=42; Max-Age=31536000; Path=/; HttpOnly",
                        "dle_password=password-hash; Max-Age=31536000; Path=/; HttpOnly"
                    ]);
                return response;
            }

            Assert.Equal("/favorites/", request.RequestUri!.AbsolutePath);
            var cookieHeader = Assert.Single(request.Headers.GetValues("Cookie"));
            Assert.Contains("PHPSESSID=session-value", cookieHeader);
            Assert.Contains("dle_user_id=42", cookieHeader);
            Assert.DoesNotContain("deleted", cookieHeader);
            return StubHttpHandler.Html(StandardAuthenticatedHtml);
        }));
        using var client = new Client("https://mirror.test", httpClient: httpClient);

        var state = await client.LoginAsync("mail@example.com", "secret");

        Assert.True(state.IsAuthenticated);
        Assert.Equal(AccountTier.Standard, state.AccountTier);
        Assert.False(state.IsPremium);
        Assert.Contains("PHPSESSID", state.CookieNames);
        Assert.Equal("42", client.Options.Cookies["dle_user_id"]);
    }

    [Fact]
    public async Task LoginAsync_ThrowsWhenSuccessResponseDoesNotCreateSession()
    {
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
            Task.FromResult(
                request.Method == HttpMethod.Post
                    ? StubHttpHandler.Json("""{"success":true}""")
                    : StubHttpHandler.Html(LoginHtml))));
        using var client = new Client("https://mirror.test", httpClient: httpClient);

        var exception = await Assert.ThrowsAsync<LoginFailedException>(
            () => client.LoginAsync("mail@example.com", "secret"));

        Assert.Contains("could not be verified", exception.Message);
    }

    [Fact]
    public async Task GetAsync_RecognizesRussianLoginPage()
    {
        using var httpClient = new HttpClient(new StubHttpHandler((_, _) =>
            Task.FromResult(StubHttpHandler.Html(LoginHtml))));
        using var client = new Client("https://mirror.test", httpClient: httpClient);

        await Assert.ThrowsAsync<LoginRequiredException>(
            () => client.GetAsync("/films/42-title.html"));
    }

    [Fact]
    public async Task LogoutAsync_InvalidatesLocalAuthenticationCookies()
    {
        var authenticated = false;
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                authenticated = true;
                var response = StubHttpHandler.Json("""{"success":true}""");
                response.Headers.TryAddWithoutValidation(
                    "Set-Cookie",
                    "dle_user_id=42; Path=/; HttpOnly");
                return Task.FromResult(response);
            }

            if (request.RequestUri!.AbsolutePath == "/logout/")
            {
                authenticated = false;
            }

            return Task.FromResult(
                StubHttpHandler.Html(
                    authenticated
                        ? "<html><head><title>Мои закладки</title></head></html>"
                        : LoginHtml));
        }));
        using var client = new Client("https://mirror.test", httpClient: httpClient);
        _ = await client.LoginAsync("mail@example.com", "secret");

        var state = await client.LogoutAsync();

        Assert.False(state.IsAuthenticated);
        Assert.DoesNotContain("dle_user_id", state.CookieNames);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_DetectsPremiumAccount()
    {
        using var httpClient = new HttpClient(new StubHttpHandler((_, _) =>
            Task.FromResult(StubHttpHandler.Html(PremiumAuthenticatedHtml))));
        using var client = new Client("https://mirror.test", httpClient: httpClient);

        var state = await client.GetAuthenticationStateAsync();

        Assert.True(state.IsAuthenticated);
        Assert.Equal(AccountTier.Premium, state.AccountTier);
        Assert.True(state.IsPremium);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_UsesUnknownTierForMalformedToken()
    {
        const string html = """
            <html>
            <head><title>Мои закладки</title></head>
            <body><input id="ctrl_token_id" value="not-a-token"></body>
            </html>
            """;
        using var httpClient = new HttpClient(new StubHttpHandler((_, _) =>
            Task.FromResult(StubHttpHandler.Html(html))));
        using var client = new Client("https://mirror.test", httpClient: httpClient);

        var state = await client.GetAuthenticationStateAsync();

        Assert.True(state.IsAuthenticated);
        Assert.Equal(AccountTier.Unknown, state.AccountTier);
        Assert.False(state.IsPremium);
    }

    private const string PageHtml = """
        <html>
        <head><title>Movie</title><meta property="og:type" content="video.movie"></head>
        <body>
          <input id="post_id" value="42">
          <h1 class="b-post__title">Title</h1>
          <div class="b-sidecover"><a href="/hq.jpg"><img src="/cover.jpg"></a></div>
          <ul id="translators-list"><li data-translator_id="56">Dub</li></ul>
        </body>
        </html>
        """;

    private const string LoginHtml = """
        <html>
        <head><title>Вход</title></head>
        <body><form action="/ajax/login/" method="post"></form></body>
        </html>
        """;

    private const string StandardAuthenticatedHtml = """
        <html>
        <head><title>Мои закладки</title></head>
        <body>
          <input id="ctrl_token_id" value="eyJhbGciOiJub25lIn0.eyJkYXRhIjp7ImlzX2xvZ2dlZCI6dHJ1ZSwibWVtYmVyX2lkIjp7ImlzX3ByZW1pdW0iOiIwIn19fQ.signature">
        </body>
        </html>
        """;

    private const string PremiumAuthenticatedHtml = """
        <html>
        <head><title>Мои закладки</title></head>
        <body>
          <input id="ctrl_token_id" value="eyJhbGciOiJub25lIn0.eyJkYXRhIjp7ImlzX2xvZ2dlZCI6dHJ1ZSwibWVtYmVyX2lkIjp7ImlzX3ByZW1pdW0iOiIxIn19fQ.signature">
        </body>
        </html>
        """;

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
