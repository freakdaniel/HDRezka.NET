namespace HdRezka.Tests;

public sealed class AccountExtendedTests
{
    [Fact]
    public async Task GeneralAndPlaybackSettings_RoundTripWebsiteFields()
    {
        var requests = new List<(string Path, string? Form)>();
        using var httpClient = new HttpClient(new StubHttpHandler(async (request, cancellationToken) =>
        {
            var form = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            requests.Add((request.RequestUri!.AbsolutePath, form));
            if (request.Method == HttpMethod.Post)
            {
                return StubHttpHandler.Html("<html><body><div class=\"b-info__message\">Saved</div></body></html>");
            }

            return StubHttpHandler.Html(
                request.RequestUri.AbsolutePath.Contains("personality", StringComparison.Ordinal)
                    ? PersonalityFormHtml
                    : GeneralFormHtml);
        }));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);

        var settings = await client.Account.GetSettingsAsync();
        Assert.Equal(AccountGender.Male, settings.Gender);
        await client.Account.UpdateSettingsAsync(
            new AccountSettings("new@example.com", AccountGender.Female));

        var preferences = await client.Account.GetPlaybackPreferencesAsync();
        Assert.True(preferences.UpdateAddressOnSelection);
        Assert.False(preferences.AutoSwitchEpisodes);
        await client.Account.UpdatePlaybackPreferencesAsync(
            new PlaybackPreferences(false, true, true));

        Assert.Contains("email=new%40example.com", requests[2].Form);
        Assert.Contains("gender=2", requests[2].Form);
        Assert.Contains("cdn_autoswitch=1", requests[5].Form);
        Assert.Contains("cdn_first_episode=1", requests[5].Form);
        Assert.DoesNotContain("ctrl_links=", requests[5].Form);
    }

    [Fact]
    public async Task PaymentReaders_ParseHistoryAndOffersWithoutCheckout()
    {
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            return Task.FromResult(StubHttpHandler.Html(
                request.RequestUri!.AbsolutePath.Contains("history", StringComparison.Ordinal)
                    ? PaymentHistoryHtml
                    : PremiumOffersHtml));
        }));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);

        var history = await client.Account.GetPaymentHistoryAsync();
        var payment = Assert.Single(history);
        Assert.Equal(PaymentStatus.Successful, payment.Status);
        Assert.Equal(180, payment.Days);

        var offers = await client.Account.GetPremiumOffersAsync("eu");
        var method = Assert.Single(offers.Methods);
        Assert.Equal("card", method.Id);
        var plan = Assert.Single(offers.Plans);
        Assert.Equal(365, plan.Days);
        Assert.Equal("€45", plan.PriceLabel);
        Assert.True(plan.IsPopular);
    }

    [Fact]
    public async Task BookmarkFiltersAndBulkMutations_UseWebsiteContracts()
    {
        var forms = new List<string>();
        using var httpClient = new HttpClient(new StubHttpHandler(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                Assert.Contains("filter=popular", request.RequestUri!.Query);
                Assert.Contains("genre=2", request.RequestUri.Query);
                return StubHttpHandler.Html(BookmarksHtml);
            }

            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            forms.Add(form);
            return form.Contains("action=change_items_cat", StringComparison.Ordinal)
                ? StubHttpHandler.Json("""{"success":true,"moved":2}""")
                : form.Contains("action=move_to_cat", StringComparison.Ordinal)
                    ? StubHttpHandler.Json("""{"success":true,"added":4}""")
                    : StubHttpHandler.Json("""{"success":true}""");
        }));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);

        var folders = await client.Account.GetBookmarksAsync(
            new BookmarkQuery(BookmarkSort.Popular, MediaCategory.Series));
        var folder = Assert.Single(folders);
        await client.Account.RenameBookmarkFolderAsync(folder, "Renamed");
        await client.Account.SortBookmarkFoldersAsync([20, 10]);
        await client.Account.RemoveBookmarksAsync(10, [100, 101]);
        var selected = await client.Account.MoveBookmarksAsync(10, 20, [100, 101]);
        var all = await client.Account.MoveBookmarkFolderAsync(10, 20);

        Assert.Equal(2, selected.Moved);
        Assert.Equal(4, all.Moved);
        Assert.Contains(forms, form => form.Contains("action=change_cat_name", StringComparison.Ordinal));
        Assert.Contains(forms, form => form.Contains("cats%5B%5D=20", StringComparison.Ordinal));
        Assert.Contains(forms, form => form.Contains("items%5B%5D=100", StringComparison.Ordinal));
    }

    private const string GeneralFormHtml = """
        <html><body><form id="userinfo" action="/user/1/">
          <input name="email" value="old@example.com">
          <select name="gender"><option value="1" selected>male</option></select>
          <input name="username_id" value="1"><input name="dle_allow_hash" value="token">
        </form></body></html>
        """;

    private const string PersonalityFormHtml = """
        <html><body><form id="userinfo" action="/user/1/personality/">
          <input type="checkbox" name="ctrl_links" value="1" checked>
          <input type="checkbox" name="cdn_autoswitch" value="1">
          <input type="checkbox" name="cdn_first_episode" value="1">
          <input name="username_id" value="1"><input name="dle_allow_hash" value="token">
        </form></body></html>
        """;

    private const string PaymentHistoryHtml = """
        <html><body><table class="b-payments_table"><tbody><tr data-url="/payments/abc/">
          <td>1</td><td>€25.00</td><td>180</td><td class="green">Успешный</td>
          <td>1 августа 2026, 12:00</td>
        </tr></tbody></table></body></html>
        """;

    private const string PremiumOffersHtml = """
        <html><body>
          <input id="card" class="payment_method-radio" name="payment_method" value="card">
          <label for="card" data-icon_url="/card.png"><div>Card</div>Bank card</label>
          <div class="pl-item"><input id="plan" type="radio" name="card-amount" value="365">
            <label for="plan"><div class="pl-title">1 year<span>Most popular</span></div>
              <div class="pl-discount">save 10%</div>
              <div class="pl-price">€45<span>~€3.75/month</span></div>
            </label>
          </div>
        </body></html>
        """;

    private const string BookmarksHtml = """
        <html><body>
          <div class="b-favorites_content__cats_list_item" data-cat_id="10">
            <a class="b-favorites_content__cats_list_link active" href="/favorites/10/">
              <span class="name">Folder</span><span class="num-holder"><b>1</b></span>
            </a>
          </div>
          <div class="b-content__inline_item" data-id="100">
            <div class="b-content__inline_item-cover"><a href="/series/100-test.html"><img src="/cover.jpg"></a><span class="cat series"></span></div>
            <div class="b-content__inline_item-link"><a href="/series/100-test.html">Series</a><div>2026, USA, Drama</div></div>
          </div>
        </body></html>
        """;
}
