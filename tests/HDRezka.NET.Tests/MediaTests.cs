using System.Text;
using System.Text.Json;

namespace HdRezka.Tests;

public sealed class MediaTests
{
    [Fact]
    public async Task CreateAsync_ParsesMovieMetadata()
    {
        using var client = CreateClient((request, _) =>
        {
            Assert.Equal("hdrezka.test", request.RequestUri!.Host);
            return Task.FromResult(StubHttpHandler.Html(MovieHtml));
        });

        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/drama/123-test.html?ref=ignored",
            httpClient: client);

        Assert.Equal(new Uri("https://hdrezka.test/films/drama/123-test.html"), media.Url);
        Assert.Equal(123, media.Id);
        Assert.Equal("Тестовый фильм", media.Name);
        Assert.Equal(["Тестовый фильм", "Test Film"], media.Names);
        Assert.Equal("Original Film", media.OriginalName);
        Assert.Equal(2024, media.ReleaseYear);
        Assert.Equal(MediaFormat.Movie, media.Format);
        Assert.Equal(MediaCategory.Film, media.Category);
        Assert.Equal(AccountTier.Standard, media.AccountTier);
        Assert.False(media.IsPremiumAccount);
        Assert.Equal(8.4, media.Rating.Value);
        Assert.Equal(1234, media.Rating.Votes);
        Assert.Equal("Дубляж (реж. версия)", media.Translators[56].Name);
        Assert.True(media.Translators[56].IsCamrip);
        Assert.True(media.Translators[56].HasAds);
        Assert.True(media.Translators[56].IsDirectorCut);
        Assert.True(media.Translators[238].IsPremium);
        Assert.Equal("Озвучка (Украинский)", media.Translators[999].Name);
        Assert.Equal(4, media.TranslationOptions.Count);
        Assert.Equal(2, media.TranslationOptions.Count(item => item.Id == 56));
        Assert.Equal("Semantic description", media.Description);
        Assert.Equal(new Uri("https://images.test/poster.jpg"), media.Thumbnail);
        Assert.Equal(new Uri("https://hdrezka.test/poster-hq.jpg"), media.ThumbnailHighQuality);
        Assert.Equal(new Uri("https://hdrezka.test/films/122-first.html"), media.OtherParts[0].Url);
        Assert.Equal(media.Url, media.OtherParts[1].Url);
    }

    [Fact]
    public async Task SetBookmarkAsync_UsesLoadedFolderStateAndUpdatesSnapshot()
    {
        var postedForms = new List<string>();
        using var client = CreateClient(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return StubHttpHandler.Html(MovieHtml);
            }

            Assert.Equal("/ajax/favorites/", request.RequestUri!.AbsolutePath);
            postedForms.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return StubHttpHandler.Json("""{"success":true}""");
        });
        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/123-test.html",
            httpClient: client);

        Assert.Equal([111], media.BookmarkFolderIds);

        await media.SetBookmarkAsync(111, isBookmarked: true);
        await media.SetBookmarkAsync(111, isBookmarked: false);
        await media.SetBookmarkAsync(111, isBookmarked: false);
        await media.SetBookmarkAsync(222, isBookmarked: true);

        Assert.Equal(2, postedForms.Count);
        Assert.Contains("post_id=123", postedForms[0]);
        Assert.Contains("cat_id=111", postedForms[0]);
        Assert.Contains("action=add_post", postedForms[0]);
        Assert.Contains("cat_id=222", postedForms[1]);
        Assert.Equal([222], media.BookmarkFolderIds);
    }

    [Fact]
    public async Task RateAsync_SubmitsVoteAndUpdatesInternalRating()
    {
        var requestNumber = 0;
        using var client = CreateClient((request, _) =>
        {
            requestNumber++;
            if (requestNumber == 1)
            {
                return Task.FromResult(StubHttpHandler.Html(MovieHtml));
            }

            Assert.Equal("/engine/ajax/rating.php", request.RequestUri!.AbsolutePath);
            Assert.Contains("news_id=123", request.RequestUri.Query);
            Assert.Contains("go_rate=9", request.RequestUri.Query);
            Assert.Contains("skin=hdrezka", request.RequestUri.Query);
            return Task.FromResult(
                StubHttpHandler.Json(
                    """{"success":true,"num":"8.41","votes":1235,"message":""}"""));
        });
        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/drama/123-test.html",
            httpClient: client);

        var rating = await media.RateAsync(9);

        Assert.Equal(8.41, rating.Value);
        Assert.Equal(1235, rating.Votes);
        Assert.Same(rating, media.Rating);
        Assert.Equal(2, requestNumber);
    }

    [Fact]
    public async Task RateAsync_ThrowsWebsiteMessageWhenVoteIsRejected()
    {
        var requestNumber = 0;
        using var client = CreateClient((request, _) =>
        {
            requestNumber++;
            return Task.FromResult(
                requestNumber == 1
                    ? StubHttpHandler.Html(MovieHtml)
                    : StubHttpHandler.Json(
                        """{"success":false,"message":"You have already voted"}"""));
        });
        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/drama/123-test.html",
            httpClient: client);

        var exception = await Assert.ThrowsAsync<RatingException>(() => media.RateAsync(9));

        Assert.Equal("You have already voted", exception.Message);
        Assert.Equal(8.4, media.Rating.Value);
    }

    [Fact]
    public async Task GetTrailerAsync_UsesCompactEndpointAndParsesEmbedUrl()
    {
        var requestNumber = 0;
        using var client = CreateClient(async (request, cancellationToken) =>
        {
            requestNumber++;
            if (requestNumber == 1)
            {
                return StubHttpHandler.Html(MovieHtml);
            }

            Assert.Equal("/engine/ajax/gettrailervideo.php", request.RequestUri!.AbsolutePath);
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.Contains("id=123", form);
            return StubHttpHandler.Json(
                """
                {
                  "success": true,
                  "title": "Official trailer",
                  "description": "Trailer description",
                  "code": "<iframe src=\"https://video.test/embed/123\"></iframe>",
                  "link": "/films/drama/123-test.html"
                }
                """);
        });
        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/drama/123-test.html",
            httpClient: client);

        var trailer = await media.GetTrailerAsync();

        Assert.Equal("Official trailer", trailer.Title);
        Assert.Equal(new Uri("https://video.test/embed/123"), trailer.SourceUrl);
        Assert.Equal(media.Url, trailer.MediaUrl);
    }

    [Fact]
    public async Task SetScheduleWatchedAsync_TogglesOnlyWhenNeeded()
    {
        var requests = 0;
        using var client = CreateClient(async (request, cancellationToken) =>
        {
            requests++;
            if (request.Method == HttpMethod.Get)
            {
                return StubHttpHandler.Html(SeriesHtml.Replace(
                    "</body>",
                    """
                    <table class="b-post__schedule_table"><tr>
                      <td class="td-1" data-id="500">1 сезон 2 серия</td>
                      <td class="td-2"><b>Episode</b></td>
                      <td><i class="watch-episode-action"></i></td>
                      <td class="td-4">26 мая 2026</td>
                      <td><span class="exists-episode"></span></td>
                    </tr></table></body>
                    """,
                    StringComparison.Ordinal));
            }

            Assert.Equal("/engine/ajax/schedule_watched.php", request.RequestUri!.AbsolutePath);
            Assert.Contains("id=500", await request.Content!.ReadAsStringAsync(cancellationToken));
            return StubHttpHandler.Json("""{"success":true}""");
        });
        using var media = await Media.CreateAsync(
            "https://hdrezka.test/series/321-show.html",
            httpClient: client);
        var entry = Assert.Single(media.Details.Schedule);

        var unchanged = await media.SetScheduleWatchedAsync(entry, false);
        var watched = await media.SetScheduleWatchedAsync(entry, true);

        Assert.Same(entry, unchanged);
        Assert.True(watched.IsWatched);
        Assert.Equal(2, requests);
    }

    [Fact]
    public async Task SortTranslators_AppliesPreferredAndNonPreferredLists()
    {
        using var client = CreateClient((_, _) =>
            Task.FromResult(StubHttpHandler.Html(MovieHtml)));
        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/123-test.html",
            httpClient: client);

        var sorted = media.SortTranslators();

        Assert.Equal([56, 56, 999, 238], sorted.Select(item => item.Id));
    }

    [Fact]
    public async Task GetStreamAsync_DecodesVideoUrlsAndSubtitles()
    {
        using var client = CreateClient(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return StubHttpHandler.Html(MovieHtml);
            }

            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.Contains("action=get_movie", form);
            Assert.Contains("translator_id=56", form);
            Assert.Contains("favs=favorite-state", form);
            Assert.Contains("is_camrip=1", form);
            Assert.Contains("is_ads=1", form);
            Assert.Contains("is_director=1", form);

            const string streams =
                "[720p]https://cdn.test/video-720.mp4 or https://backup.test/video-720.mp4," +
                "[1080p <b>High</b>]https://cdn.test/video-1080.mp4 or " +
                "https://cdn.test/video-1080.mp4:hls:manifest.m3u8";
            var response = JsonSerializer.Serialize(new
            {
                success = true,
                url = Convert.ToBase64String(Encoding.UTF8.GetBytes(streams)),
                quality = "1080p High",
                subtitle = "[English]https://cdn.test/en.vtt,[Русский]https://cdn.test/ru.vtt",
                subtitle_lns = new Dictionary<string, string>
                {
                    ["English"] = "en",
                    ["Русский"] = "ru"
                },
                subtitle_def = "en",
                thumbnails = "/tiles/123.jpg",
                premium_content = false
            });
            return StubHttpHandler.Json(response);
        });

        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/123-test.html",
            httpClient: client);
        var stream = await media.GetStreamAsync();

        Assert.Equal(56, stream.TranslatorId);
        Assert.Equal(2, stream.GetUrls("720").Count);
        Assert.Equal(
            new Uri("https://cdn.test/video-1080.mp4"),
            stream.GetUrls("High")[0]);
        Assert.Contains(
            new Uri("https://cdn.test/video-1080.mp4:hls:manifest.m3u8"),
            stream.GetUrls("High"));
        Assert.Equal(new Uri("https://cdn.test/en.vtt"), stream.Subtitles.GetUrl("en"));
        Assert.Equal(new Uri("https://cdn.test/ru.vtt"), stream.Subtitles.GetUrl("Русский"));
        Assert.Equal(new Uri("https://cdn.test/en.vtt"), stream.Subtitles.GetUrl(0));
        Assert.Equal("1080p High", stream.DefaultQuality);
        Assert.Equal("en", stream.DefaultSubtitle);
        Assert.Equal(new Uri("https://hdrezka.test/tiles/123.jpg"), stream.ThumbnailPreview);
        Assert.False(stream.IsPremiumContent);
    }

    [Fact]
    public async Task GetStreamAsync_HidesPremiumQualityUrlsFromStandardAccount()
    {
        using var client = CreateClient((request, _) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(StubHttpHandler.Html(MovieHtml));
            }

            const string streams =
                "[1080p]https://cdn.test/video-1080.mp4," +
                "[<span class=\"pjs-prem-quality\">1080p Ultra<img src=\"premium.svg\"></span>]" +
                "https://cdn.test/video-ultra.mp4," +
                "[2K]https://cdn.test/video-2k.mp4," +
                "[4K]https://cdn.test/video-4k.mp4";
            return Task.FromResult(StubHttpHandler.Json(JsonSerializer.Serialize(new
            {
                success = true,
                url = streams,
                subtitle = "",
                subtitle_lns = new Dictionary<string, string>(),
                premium_content = false
            })));
        });
        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/123-test.html",
            httpClient: client);

        var stream = await media.GetStreamAsync(translation: "56");

        Assert.Equal(["1080p"], stream.Videos.Keys);
        Assert.False(stream.Qualities["1080p Ultra"].IsAvailable);
        Assert.False(stream.Qualities["2K"].IsAvailable);
        Assert.False(stream.Qualities["4K"].IsAvailable);
        Assert.Empty(stream.Qualities["4K"].Urls);
        var exception = Assert.Throws<PremiumRequiredException>(() => stream.GetUrls("4K"));
        Assert.Equal(PremiumFeature.Quality, exception.Feature);
        Assert.Equal("4K", exception.Name);
    }

    [Fact]
    public async Task GetStreamAsync_ExposesPremiumQualityUrlsToPremiumAccount()
    {
        using var client = CreateClient((request, _) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(StubHttpHandler.Html(PremiumMovieHtml));
            }

            return Task.FromResult(StubHttpHandler.Json(JsonSerializer.Serialize(new
            {
                success = true,
                url = "[4K]https://cdn.test/video-4k.mp4",
                subtitle = "",
                subtitle_lns = new Dictionary<string, string>(),
                premium_content = false
            })));
        });
        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/123-test.html",
            httpClient: client);

        var stream = await media.GetStreamAsync(translation: "238");

        Assert.True(media.IsPremiumAccount);
        Assert.Equal(238, stream.TranslatorId);
        Assert.True(stream.Qualities["4K"].RequiresPremium);
        Assert.True(stream.Qualities["4K"].IsAvailable);
        Assert.Equal(
            new Uri("https://cdn.test/video-4k.mp4"),
            Assert.Single(stream.GetUrls("4K")));
    }

    [Fact]
    public async Task GetStreamAsync_RejectsPremiumTranslationBeforePlayerRequest()
    {
        var postRequests = 0;
        using var client = CreateClient((request, _) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                postRequests++;
            }

            return Task.FromResult(StubHttpHandler.Html(MovieHtml));
        });
        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/123-test.html",
            httpClient: client);

        var exception = await Assert.ThrowsAsync<PremiumRequiredException>(
            () => media.GetStreamAsync(translation: "238"));

        Assert.Equal(0, postRequests);
        Assert.Equal(PremiumFeature.Translation, exception.Feature);
        Assert.Equal("Оригинал + субтитры", exception.Name);
    }

    [Fact]
    public async Task GetStreamAsync_RejectsPremiumResponseEvenWhenItContainsUrls()
    {
        using var client = CreateClient((request, _) =>
            Task.FromResult(
                request.Method == HttpMethod.Get
                    ? StubHttpHandler.Html(MovieHtml)
                    : StubHttpHandler.Json(JsonSerializer.Serialize(new
                    {
                        success = true,
                        url = "[720p]https://cdn.test/should-not-be-exposed.mp4",
                        premium_content = true
                    }))));
        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/123-test.html",
            httpClient: client);

        var exception = await Assert.ThrowsAsync<PremiumRequiredException>(
            () => media.GetStreamAsync(translation: "56"));

        Assert.Equal(PremiumFeature.Content, exception.Feature);
    }

    [Fact]
    public async Task GetStreamAsync_AutomaticSelectionSkipsPremiumTranslation()
    {
        string? playerForm = null;
        using var client = CreateClient(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return StubHttpHandler.Html(MovieHtml);
            }

            playerForm = await request.Content!.ReadAsStringAsync(cancellationToken);
            return StubHttpHandler.Json(JsonSerializer.Serialize(new
            {
                success = true,
                url = "[720p]https://cdn.test/video.mp4"
            }));
        });
        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/123-test.html",
            httpClient: client);
        media.PreferredTranslators.Clear();
        media.PreferredTranslators.Add(238);
        media.PreferredTranslators.Add(56);

        _ = await media.GetStreamAsync();

        Assert.Contains("translator_id=56", playerForm);
        Assert.DoesNotContain("translator_id=238", playerForm);
    }

    [Fact]
    public async Task GetStreamAsync_UsesFlagsFromNamedTranslatorVariant()
    {
        string? playerForm = null;
        using var client = CreateClient(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return StubHttpHandler.Html(MovieHtml);
            }

            playerForm = await request.Content!.ReadAsStringAsync(cancellationToken);
            return StubHttpHandler.Json(JsonSerializer.Serialize(new
            {
                success = true,
                url = "[720p]https://cdn.test/video.mp4"
            }));
        });
        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/123-test.html",
            httpClient: client);

        _ = await media.GetStreamAsync(translation: "Дубляж");

        Assert.Contains("translator_id=56", playerForm);
        Assert.Contains("is_camrip=0", playerForm);
        Assert.Contains("is_ads=0", playerForm);
        Assert.Contains("is_director=0", playerForm);
    }

    [Fact]
    public async Task SeriesMethods_MergeEpisodesAndFetchRequestedEpisode()
    {
        var episodeRequests = 0;
        using var client = CreateClient(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return StubHttpHandler.Html(SeriesHtml);
            }

            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (form.Contains("action=get_episodes", StringComparison.Ordinal))
            {
                episodeRequests++;
                return StubHttpHandler.Json(JsonSerializer.Serialize(new
                {
                    success = true,
                    seasons =
                        "<li class=\"b-simple_season__item\" data-tab_id=\"1\">Season 1</li>",
                    episodes =
                        "<li class=\"b-simple_episode__item\" data-season_id=\"1\" " +
                        "data-episode_id=\"1\">Episode 1</li>" +
                        "<li class=\"b-simple_episode__item\" data-season_id=\"1\" " +
                        "data-episode_id=\"2\">Episode 2</li>"
                }));
            }

            Assert.Contains("action=get_stream", form);
            Assert.Contains("season=1", form);
            Assert.Contains("episode=2", form);
            return StubHttpHandler.Json(JsonSerializer.Serialize(new
            {
                success = true,
                url = "[720p]https://cdn.test/s01e02.mp4",
                subtitle = "",
                subtitle_lns = new Dictionary<string, string>()
            }));
        });

        using var media = await Media.CreateAsync(
            "https://hdrezka.test/series/321-show.html",
            httpClient: client);
        var seasons = await media.GetEpisodesInfoAsync();
        var cachedSeasons = await media.GetEpisodesInfoAsync();
        var stream = await media.GetStreamAsync(1, 2);

        Assert.Same(seasons, cachedSeasons);
        Assert.Equal(1, episodeRequests);
        Assert.Equal(2, Assert.Single(seasons).Episodes.Count);
        Assert.Equal(2, stream.Episode);
        Assert.Equal(new Uri("https://cdn.test/s01e02.mp4"), Assert.Single(stream.GetUrls("720")));
    }

    [Fact]
    public async Task GetStreamAsync_LoadsOnlyRequestedSeriesTranslator()
    {
        var requestedTranslators = new List<string>();
        using var client = CreateClient(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return StubHttpHandler.Html(MultiTranslatorSeriesHtml);
            }

            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            requestedTranslators.Add(form);
            if (form.Contains("action=get_episodes", StringComparison.Ordinal))
            {
                return StubHttpHandler.Json(JsonSerializer.Serialize(new
                {
                    success = true,
                    seasons =
                        "<li class=\"b-simple_season__item\" data-tab_id=\"1\">Season 1</li>",
                    episodes =
                        "<li class=\"b-simple_episode__item\" data-season_id=\"1\" " +
                        "data-episode_id=\"2\">Episode 2</li>"
                }));
            }

            return StubHttpHandler.Json(JsonSerializer.Serialize(new
            {
                success = true,
                url = "[720p]https://cdn.test/second-s01e02.mp4",
                subtitle = "",
                subtitle_lns = new Dictionary<string, string>()
            }));
        });

        using var media = await Media.CreateAsync(
            "https://hdrezka.test/series/654-show.html",
            httpClient: client);
        var info = await media.GetSeriesInfoAsync("Second");
        var stream = await media.GetStreamAsync(1, 2, "Second");

        Assert.Equal(2, requestedTranslators.Count);
        Assert.All(
            requestedTranslators,
            form => Assert.Contains("translator_id=2", form));
        Assert.DoesNotContain(
            requestedTranslators,
            form => form.Contains("translator_id=1", StringComparison.Ordinal));
        Assert.Equal(2, info.TranslatorId);
        Assert.Equal(2, stream.TranslatorId);
    }

    [Fact]
    public async Task GetStreamAsync_ReusesInitialStreamFromEpisodeResponse()
    {
        var postRequests = 0;
        using var client = CreateClient(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return StubHttpHandler.Html(MultiTranslatorSeriesHtml);
            }

            postRequests++;
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.Contains("action=get_episodes", form);
            Assert.Contains("translator_id=2", form);
            return StubHttpHandler.Json(JsonSerializer.Serialize(new
            {
                success = true,
                seasons =
                    "<li class=\"b-simple_season__item active\" data-tab_id=\"1\">Season 1</li>",
                episodes =
                    "<li class=\"b-simple_episode__item active\" data-season_id=\"1\" " +
                    "data-episode_id=\"1\">Episode 1</li>",
                url = "[720p]https://cdn.test/second-s01e01.mp4",
                quality = "720p",
                subtitle = "",
                subtitle_lns = new Dictionary<string, string>()
            }));
        });

        using var media = await Media.CreateAsync(
            "https://hdrezka.test/series/654-show.html",
            httpClient: client);
        var stream = await media.GetStreamAsync(1, 1, "Second");

        Assert.Equal(1, postRequests);
        Assert.Equal(new Uri("https://cdn.test/second-s01e01.mp4"), Assert.Single(stream.GetUrls("720")));
    }

    [Fact]
    public async Task GetStreamAsync_ReusesEpisodeCatalogFromMediaPage()
    {
        var postRequests = 0;
        using var client = CreateClient(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return StubHttpHandler.Html(SeriesPageWithEpisodesHtml);
            }

            postRequests++;
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.Contains("action=get_stream", form);
            Assert.DoesNotContain("action=get_episodes", form);
            return StubHttpHandler.Json(JsonSerializer.Serialize(new
            {
                success = true,
                url = "[720p]https://cdn.test/page-catalog-s01e01.mp4",
                subtitle = "",
                subtitle_lns = new Dictionary<string, string>()
            }));
        });

        using var media = await Media.CreateAsync(
            "https://hdrezka.test/series/888-page-catalog.html",
            httpClient: client);
        var stream = await media.GetStreamAsync(1, 1);

        Assert.Equal(1, postRequests);
        Assert.Equal(new Uri("https://cdn.test/page-catalog-s01e01.mp4"), Assert.Single(stream.GetUrls("720")));
    }

    [Fact]
    public async Task GetSeasonStreamsAsync_UsesLimitedParallelRequests()
    {
        var active = 0;
        var maximumActive = 0;
        var twoRequestsStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRequests = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = CreateClient(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return StubHttpHandler.Html(SeriesHtml);
            }

            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (form.Contains("action=get_episodes", StringComparison.Ordinal))
            {
                return StubHttpHandler.Json(JsonSerializer.Serialize(new
                {
                    success = true,
                    seasons =
                        "<li class=\"b-simple_season__item\" data-tab_id=\"1\">Season 1</li>",
                    episodes = string.Concat(
                        Enumerable.Range(1, 4).Select(
                            episode =>
                                "<li class=\"b-simple_episode__item\" data-season_id=\"1\" " +
                                $"data-episode_id=\"{episode}\">Episode {episode}</li>"))
                }));
            }

            var current = Interlocked.Increment(ref active);
            var observedMaximum = Volatile.Read(ref maximumActive);
            while (current > observedMaximum)
            {
                var original = Interlocked.CompareExchange(
                    ref maximumActive,
                    current,
                    observedMaximum);
                if (original == observedMaximum)
                {
                    break;
                }

                observedMaximum = original;
            }

            if (current == 2)
            {
                twoRequestsStarted.TrySetResult(true);
            }

            await releaseRequests.Task.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref active);
            var episodeNumber = Enumerable.Range(1, 4)
                .Single(episode => form.Contains($"episode={episode}", StringComparison.Ordinal));
            return StubHttpHandler.Json(JsonSerializer.Serialize(new
            {
                success = true,
                url = $"[720p]https://cdn.test/s01e{episodeNumber:00}.mp4"
            }));
        });
        var options = new ClientOptions { MaxConcurrentRequests = 2 };
        using var media = await Media.CreateAsync(
            "https://hdrezka.test/series/321-show.html",
            options,
            client);

        var streamsTask = media.GetSeasonStreamsAsync(1);
        await twoRequestsStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, Volatile.Read(ref active));
        releaseRequests.TrySetResult(true);
        var streams = await streamsTask;

        Assert.Equal(4, streams.Count);
        Assert.Equal(2, maximumActive);
        Assert.All(streams.Values, Assert.NotNull);
    }

    [Fact]
    public async Task CreateAsync_UsesOpenGraphWhenPageSelectorsAreMissing()
    {
        const string html = """
            <html>
            <head>
              <meta property="og:type" content="video.movie">
              <meta property="og:title" content="Fallback title (2025)">
              <meta property="og:description" content="Fallback description">
              <meta property="og:image" content="https://images.test/fallback.jpg">
            </head>
            <body>
              <input id="post_id" value="777">
              <ul id="translators-list"><li data-translator_id="56">Dub</li></ul>
            </body>
            </html>
            """;
        using var client = CreateClient((_, _) =>
            Task.FromResult(StubHttpHandler.Html(html)));

        using var media = await Media.CreateAsync(
            "https://hdrezka.test/films/777-fallback.html",
            httpClient: client);

        Assert.Equal("Fallback title", media.Name);
        Assert.Equal("Fallback description", media.Description);
        Assert.Equal(2025, media.ReleaseYear);
        Assert.Equal(new Uri("https://images.test/fallback.jpg"), media.Thumbnail);
    }

    [Fact]
    public async Task CreateAsync_ParsesExtendedMetadataWithoutAdditionalRequests()
    {
        var requests = 0;
        const string html = """
            <html>
            <head>
              <meta property="og:type" content="video.tv_series">
              <meta property="og:image" content="/show.jpg">
            </head>
            <body>
              <input id="post_id" value="700">
              <h1 class="b-post__title">Detailed show</h1>
              <ul id="translators-list"><li data-translator_id="56">Dub</li></ul>
              <table class="b-post__info">
                <tr><td class="l"><h2>Рейтинги</h2></td><td>
                  <span class="b-post__info_rates imdb">
                    <a href="/external/imdb">IMDb</a>
                    <span class="bold">8.1</span><i>(12 345)</i>
                  </span>
                </td></tr>
                <tr><td class="l"><h2>Входит в списки</h2></td>
                  <td><a href="/best/series/">Best series</a></td></tr>
                <tr><td class="l"><h2>Слоган</h2></td><td>Test tagline</td></tr>
                <tr><td class="l"><h2>Дата выхода</h2></td>
                  <td>13 мая <a href="/year/2026/">2026 года</a></td></tr>
                <tr><td class="l"><h2>Страна</h2></td>
                  <td><a href="/country/USA/">США</a></td></tr>
                <tr><td class="l"><h2>Режиссер</h2></td><td>
                  <span class="person-name-item" itemprop="director"
                    data-id="10" data-job="Режиссер" data-photo="/people/10.jpg">
                    <a href="/person/10-director/"><span itemprop="name">Director</span></a>
                  </span>
                </td></tr>
                <tr><td class="l"><h2>Жанр</h2></td><td>
                  <a href="/series/drama/"><span itemprop="genre">Драмы</span></a>
                </td></tr>
                <tr><td class="l"><h2>В качестве</h2></td><td>1080p</td></tr>
                <tr><td class="l"><h2>Возраст</h2></td><td>16+</td></tr>
                <tr><td class="l"><h2>Время</h2></td><td>45 мин.</td></tr>
                <tr><td class="l"><h2>Из серии</h2></td>
                  <td><a href="/collections/1-test/">Test collection</a></td></tr>
                <tr><td colspan="2"><h2>В ролях актеры</h2>
                  <span class="person-name-item" itemprop="actor"
                    data-id="20" data-job="Актер">
                    <a href="/person/20-actor/"><span itemprop="name">Actor</span></a>
                  </span>
                </td></tr>
              </table>
              <table class="b-post__schedule_table">
                <tr>
                  <td class="td-1" data-id="500">1 сезон 2 серия</td>
                  <td class="td-2"><b>Episode title</b><span>Original title</span></td>
                  <td class="td-3"><i class="watch-episode-action watched"></i></td>
                  <td class="td-4">26 мая 2026</td>
                  <td class="td-5"><span class="exists-episode">✓</span></td>
                </tr>
              </table>
              <div class="b-content__inline_item" data-id="701">
                <div class="b-content__inline_item-cover">
                  <a href="/films/drama/701-related.html"><img src="/related.jpg"></a>
                  <span class="cat films"></span>
                </div>
                <div class="b-content__inline_item-link">
                  <a href="/films/drama/701-related.html">Related movie</a>
                  <div>2025, USA, Drama</div>
                </div>
              </div>
            </body>
            </html>
            """;
        using var client = CreateClient((_, _) =>
        {
            requests++;
            return Task.FromResult(StubHttpHandler.Html(html));
        });

        using var media = await Media.CreateAsync(
            "https://hdrezka.test/series/700-detailed.html",
            httpClient: client);

        Assert.Equal(1, requests);
        Assert.Equal("Test tagline", media.Details.Tagline);
        Assert.Equal(new DateOnly(2026, 5, 13), media.Details.ReleaseDate);
        Assert.Equal("США", Assert.Single(media.Details.Countries).Name);
        Assert.Equal("Драмы", Assert.Single(media.Details.Genres).Name);
        Assert.Equal("Director", Assert.Single(media.Details.Directors).Name);
        Assert.Equal("Actor", Assert.Single(media.Details.Cast).Name);
        Assert.Equal("1080p", media.Details.Quality);
        Assert.Equal("16+", media.Details.AgeRating);
        Assert.Equal(TimeSpan.FromMinutes(45), media.Details.Duration);
        Assert.Equal("Test collection", Assert.Single(media.Details.Collections).Name);
        Assert.Equal("Best series", Assert.Single(media.Details.Rankings).Name);
        var externalRating = Assert.Single(media.Details.ExternalRatings);
        Assert.Equal("IMDb", externalRating.Source);
        Assert.Equal(8.1, externalRating.Value);
        Assert.Equal(12345, externalRating.Votes);
        Assert.Equal("Related movie", Assert.Single(media.Details.Recommendations).Title);
        var schedule = Assert.Single(media.Details.Schedule);
        Assert.Equal(500, schedule.Id);
        Assert.Equal(1, schedule.Season);
        Assert.Equal(2, schedule.Episode);
        Assert.Equal(new DateOnly(2026, 5, 26), schedule.ReleaseDate);
        Assert.True(schedule.IsAvailable);
        Assert.True(schedule.IsWatched);
    }

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) =>
        new(new StubHttpHandler(handler));

    private static string MovieHtml =>
        MovieHtmlTemplate.Replace("{account-token}", StandardAccountToken, StringComparison.Ordinal);

    private static string PremiumMovieHtml =>
        MovieHtmlTemplate.Replace("{account-token}", PremiumAccountToken, StringComparison.Ordinal);

    private const string MovieHtmlTemplate = """
        <!doctype html>
        <html>
        <head>
          <title>Movie</title>
          <meta property="og:type" content="video.movie">
          <meta property="og:image" content="https://images.test/poster.jpg">
          <meta itemprop="description" content="Semantic description">
        </head>
        <body>
          <input id="ctrl_token_id" value="{account-token}">
          <main class="b-content__main">
            <input id="post_id" value="123">
            <input id="ctrl_favs" value="favorite-state">
            <div id="user-favorites-holder">
              <input class="user-fav-check" value="111" checked="checked">
              <input class="user-fav-check" value="222">
            </div>
            <h1 class="b-post__title">Тестовый фильм / Test Film</h1>
            <div class="b-post__origtitle">Film Original / Original Film</div>
            <div class="b-post__description_text"> Description text. </div>
            <div class="b-sidecover">
              <a href="/poster-hq.jpg"><img src="/poster.jpg"></a>
            </div>
            <table class="b-post__info"><tr><td><a href="/year/2024/">2024</a></td></tr></table>
            <div class="b-post__rating">
              <span class="num">8.4</span><span class="votes">(1 234)</span>
            </div>
            <ul id="translators-list">
              <li data-translator_id="238" class="b-prem_translator">Оригинал + субтитры</li>
              <li data-translator_id="999">Озвучка <img title="Украинский"></li>
              <li data-translator_id="56" data-camrip="1" data-ads="1" data-director="1">Дубляж (реж. версия)</li>
              <li data-translator_id="56">Дубляж</li>
            </ul>
            <div class="b-post__partcontent">
              <div class="b-post__partcontent_item" data-url="/films/122-first.html">
                <span class="title">Part one</span>
              </div>
              <div class="b-post__partcontent_item current">
                <span class="title">Part two</span>
              </div>
            </div>
          </main>
        </body>
        </html>
        """;

    private const string StandardAccountToken =
        "eyJhbGciOiJub25lIn0.eyJkYXRhIjp7ImlzX2xvZ2dlZCI6dHJ1ZSwibWVtYmVyX2lkIjp7ImlzX3ByZW1pdW0iOiIwIn19fQ.signature";

    private const string PremiumAccountToken =
        "eyJhbGciOiJub25lIn0.eyJkYXRhIjp7ImlzX2xvZ2dlZCI6dHJ1ZSwibWVtYmVyX2lkIjp7ImlzX3ByZW1pdW0iOiIxIn19fQ.signature";

    private const string SeriesHtml = """
        <!doctype html>
        <html>
        <head>
          <title>Series</title>
          <meta property="og:type" content="video.tv_series">
        </head>
        <body>
          <input id="post_id" value="321">
          <h1 class="b-post__title">Test Show</h1>
          <div class="b-post__description_text">A show.</div>
          <div class="b-sidecover"><a href="/show-hq.jpg"><img src="/show.jpg"></a></div>
          <ul id="translators-list"><li data-translator_id="56">Дубляж</li></ul>
        </body>
        </html>
        """;

    private const string MultiTranslatorSeriesHtml = """
        <!doctype html>
        <html>
        <head>
          <title>Series</title>
          <meta property="og:type" content="video.tv_series">
          <meta property="og:image" content="/show.jpg">
        </head>
        <body>
          <input id="post_id" value="654">
          <input id="ctrl_favs" value="series-state">
          <h1 class="b-post__title">Test Show</h1>
          <div class="b-post__description_text">A show.</div>
          <ul id="translators-list">
            <li data-translator_id="1">First</li>
            <li data-translator_id="2">Second</li>
          </ul>
        </body>
        </html>
        """;

    private const string SeriesPageWithEpisodesHtml = """
        <!doctype html>
        <html>
        <head>
          <title>Series</title>
          <meta property="og:type" content="video.tv_series">
          <meta property="og:image" content="/show.jpg">
        </head>
        <body>
          <input id="post_id" value="888">
          <h1 class="b-post__title">Page catalog show</h1>
          <ul id="translators-list">
            <li class="active" data-translator_id="56">Dub</li>
          </ul>
          <div class="b-simple_season__item active" data-tab_id="1">Season 1</div>
          <div class="b-simple_episode__item active" data-season_id="1" data-episode_id="1">
            Episode 1
          </div>
        </body>
        </html>
        """;
}
