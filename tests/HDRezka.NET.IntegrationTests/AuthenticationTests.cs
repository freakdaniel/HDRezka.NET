using System.Globalization;
using System.Net;

namespace HdRezka.IntegrationTests;

public sealed class AuthenticationTests
{
    [Fact]
    [Trait("Category", "Live")]
    public async Task Login_LoadMedia_AndLogout()
    {
        var email = Environment.GetEnvironmentVariable("HDREZKA_TEST_EMAIL");
        var password = Environment.GetEnvironmentVariable("HDREZKA_TEST_PASSWORD");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var origin = Environment.GetEnvironmentVariable("HDREZKA_TEST_ORIGIN")
            ?? "https://hdrezka.fi";
        var mediaPath = Environment.GetEnvironmentVariable("HDREZKA_TEST_MEDIA_PATH")
            ?? "/films/thriller/85998-normal-2025-latest.html";
        var seriesPath = Environment.GetEnvironmentVariable("HDREZKA_TEST_SERIES_PATH")
            ?? "/series/fiction/89858-pauk-nuar-2026-latest.html";

        using var client = new Client(origin);
        var login = await client.LoginAsync(email, password);

        Assert.True(login.IsAuthenticated);
        Assert.Equal(login.AccountTier == AccountTier.Premium, login.IsPremium);
        Assert.Contains("PHPSESSID", login.CookieNames);
        Assert.Contains("dle_user_id", login.CookieNames);
        Assert.Contains("dle_password", login.CookieNames);

        var profile = await client.Account.GetProfileAsync();
        Assert.True(profile.Id > 0);
        Assert.False(string.IsNullOrWhiteSpace(profile.Username));
        Assert.NotNull(profile.AvatarUrl);
        Assert.Equal(login.AccountTier, profile.Tier);

        var continueWatching = await client.Account.GetContinueWatchingAsync();
        Assert.NotEmpty(continueWatching);
        Assert.All(continueWatching, item =>
        {
            Assert.True(item.Id > 0);
            Assert.True(item.Url.IsAbsoluteUri);
            Assert.True(item.ImageUrl.IsAbsoluteUri);
        });

        var bookmarkFolders = await client.Account.GetBookmarksAsync();
        Assert.NotEmpty(bookmarkFolders);
        Assert.All(bookmarkFolders, folder => Assert.True(folder.Url.IsAbsoluteUri));

        var catalogPages = await Task.WhenAll(
            client.Catalog.GetLatestAsync(),
            client.Catalog.GetPopularAsync(),
            client.Catalog.GetUpcomingAsync(),
            client.Catalog.GetWatchingAsync(),
            client.Catalog.GetNewReleasesAsync(),
            client.Catalog.GetAnnouncementsAsync(),
            client.Catalog.GetShowsAsync());
        Assert.All(catalogPages, page => Assert.NotEmpty(page.Items));

        var collections = await client.Collections.GetPageAsync();
        Assert.NotEmpty(collections.Items);
        CollectionPage? collection = null;
        CollectionSummary? loadedCollection = null;
        foreach (var item in collections.Items.Take(10))
        {
            try
            {
                collection = await client.Collections.GetAsync(item);
                loadedCollection = item;
                break;
            }
            catch (HttpException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                // The authenticated collection directory can temporarily contain links
                // that the same authenticated session receives as 404
            }
        }

        Assert.NotNull(collection);
        Assert.NotNull(loadedCollection);
        Assert.Equal(loadedCollection.Id, collection.Id);
        Assert.NotEmpty(collection.Items);

        using var media = await client.GetAsync(mediaPath);
        Assert.True(media.Id > 0);
        Assert.False(string.IsNullOrWhiteSpace(media.Name));
        Assert.NotEmpty(media.Translators);
        Assert.NotEqual(AccountTier.Unknown, media.AccountTier);
        Assert.NotEmpty(media.Details.Countries);
        Assert.NotEmpty(media.Details.Genres);
        Assert.NotEmpty(media.Details.Directors);
        Assert.NotEmpty(media.Details.Cast);
        Assert.NotNull(media.Details.Duration);

        var comments = await media.Comments.GetPageAsync();
        Assert.NotEmpty(comments.Items);

        var stream = await media.GetStreamAsync();
        Assert.NotEmpty(stream.Videos);

        using var series = await client.GetAsync(seriesPath);
        Assert.Equal(MediaFormat.Series, series.Format);
        Assert.NotEmpty(series.Details.Schedule);
        var translator = series.SortTranslators().First(item => !item.IsPremium);
        var seriesInfo = await series.GetSeriesInfoAsync(
            translator.Id.ToString(CultureInfo.InvariantCulture));
        var firstSeason = seriesInfo.Seasons.Keys.Order().First();
        var firstEpisode = seriesInfo.Episodes[firstSeason].Keys.Order().First();
        var episodeStream = await series.GetStreamAsync(
            firstSeason,
            firstEpisode,
            translator.Id.ToString(CultureInfo.InvariantCulture));
        Assert.NotEmpty(episodeStream.Videos);

        using var premiumMedia = await client.GetAsync(
            "/films/comedy/87262-ochen-strashnoe-kino-2026-latest.html");
        Assert.Contains(premiumMedia.TranslationOptions, item => item.IsPremium);
        var regularStream = await premiumMedia.GetStreamAsync();
        var premiumQualities = regularStream.Qualities.Values
            .Where(quality => quality.RequiresPremium)
            .ToList();
        Assert.Contains(premiumQualities, quality => quality.Name == "1080p Ultra");

        if (premiumMedia.AccountTier == AccountTier.Standard)
        {
            var premiumTranslator = premiumMedia.TranslationOptions.First(item => item.IsPremium);
            var premiumException = await Assert.ThrowsAsync<PremiumRequiredException>(
                () => premiumMedia.GetStreamAsync(translation: premiumTranslator.Name));
            Assert.Equal(PremiumFeature.Translation, premiumException.Feature);
            Assert.All(premiumQualities, quality =>
            {
                Assert.False(quality.IsAvailable);
                Assert.Empty(quality.Urls);
            });
        }

        var logout = await client.LogoutAsync();
        Assert.False(logout.IsAuthenticated);
    }
}
