using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using HdRezka.Abstractions;
using HdRezka.Http;
using HdRezka.Scraping;
using HdRezka.Translators;

namespace HdRezka;

/// <summary>
/// Holds parsed metadata for one movie or series and provides access to its streams and episodes
/// </summary>
public sealed class Media : IDisposable
{
    private readonly HttpTransport _transport;
    private readonly IScraper _scraper;
    private readonly bool _ownsTransport;
    private readonly string _favorites;
    private readonly object _cacheLock = new();
    private readonly object _bookmarkLock = new();
    private readonly SemaphoreSlim _bookmarkMutationLock = new(1, 1);
    private readonly HashSet<long> _bookmarkFolderIds;
    private readonly Dictionary<TranslatorKey, Task<SeriesInfo?>> _seriesInfoByTranslator = [];
    private readonly Dictionary<StreamKey, MediaStream> _streamCache = [];
    private Task<IReadOnlyDictionary<int, SeriesInfo>>? _seriesInfoTask;
    private Task<IReadOnlyList<Season>>? _episodesInfoTask;

    private Media(
        HttpTransport transport,
        bool ownsTransport,
        Uri url,
        PageSnapshot page,
        IScraper scraper)
    {
        _transport = transport;
        _scraper = scraper;
        _ownsTransport = ownsTransport;
        Url = url;
        Origin = new Uri(url.GetLeftPart(UriPartial.Authority));

        Id = page.Id;
        Comments = new CommentClient(_transport, Url, Id);
        Names = page.Names;
        Name = page.Name;
        OriginalNames = page.OriginalNames;
        OriginalName = page.OriginalName;
        Description = page.Description;
        ShortDescription = page.ShortDescription;
        Thumbnail = page.Thumbnail;
        ThumbnailHighQuality = page.ThumbnailHighQuality;
        ReleaseYear = page.ReleaseYear;
        Format = page.Format;
        Category = page.Category;
        Details = page.Details;
        Rating = page.Rating;
        AccountTier = page.AccountTier;
        Playback = page.Playback;
        TranslationOptions = page.TranslationOptions;
        Translators = page.Translators;
        TranslatorsByName = page.TranslatorsByName;
        OtherParts = page.OtherParts;
        _bookmarkFolderIds = [.. page.BookmarkFolderIds];
        _favorites = page.Favorites;

        if (page.InitialSeriesInfo is not null)
        {
            var initialTranslator = TranslationOptions.FirstOrDefault(
                translator => translator.Id == page.InitialSeriesInfo.TranslatorId);
            if (initialTranslator is not null)
            {
                _seriesInfoByTranslator[CreateTranslatorKey(initialTranslator)] =
                    Task.FromResult<SeriesInfo?>(page.InitialSeriesInfo);
            }
        }
    }

    /// <summary>
    /// Gets the normalized page URL without query parameters or fragments after the <c>.html</c> suffix
    /// </summary>
    public Uri Url { get; }

    /// <summary>
    /// Gets the scheme and host used for related requests
    /// </summary>
    public Uri Origin { get; }

    /// <summary>
    /// Gets the numeric media identifier used by the website API
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets bookmark folder identifiers selected on the loaded media page
    /// </summary>
    /// <value>
    /// Snapshot of folder identifiers for the authenticated account
    /// </value>
    public IReadOnlyCollection<long> BookmarkFolderIds
    {
        get
        {
            lock (_bookmarkLock)
            {
                return _bookmarkFolderIds.ToArray();
            }
        }
    }

    /// <summary>
    /// Gets operations for loading paginated comments through the website AJAX endpoint
    /// </summary>
    public CommentClient Comments { get; }

    /// <summary>
    /// Gets the first localized title shown on the page
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets all localized titles parsed from the main page heading
    /// </summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>
    /// Gets the last original title shown on the page
    /// </summary>
    /// <value>
    /// Original title, or <see langword="null"/> when the page does not provide one
    /// </value>
    public string? OriginalName { get; }

    /// <summary>
    /// Gets all original titles shown on the page
    /// </summary>
    public IReadOnlyList<string> OriginalNames { get; }

    /// <summary>
    /// Gets the media description with surrounding whitespace removed
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the shorter description provided by the page metadata
    /// </summary>
    /// <value>
    /// Short description, or <see langword="null"/> when the page does not provide one separately
    /// </value>
    public string? ShortDescription { get; }

    /// <summary>
    /// Gets the thumbnail URL shown directly on the media page
    /// </summary>
    public Uri Thumbnail { get; }

    /// <summary>
    /// Gets the high-quality thumbnail linked by the media page
    /// </summary>
    /// <value>
    /// High-quality thumbnail URL, or <see langword="null"/> when no separate image is available
    /// </value>
    public Uri? ThumbnailHighQuality { get; }

    /// <summary>
    /// Gets the release year parsed from the media details
    /// </summary>
    /// <value>
    /// Four-digit release year, or <see langword="null"/> when the page does not provide one
    /// </value>
    public int? ReleaseYear { get; }

    /// <summary>
    /// Gets whether the page represents a movie, a series, or an unknown format
    /// </summary>
    public MediaFormat Format { get; }

    /// <summary>
    /// Gets the catalog category inferred from the page URL
    /// </summary>
    public MediaCategory Category { get; }

    /// <summary>
    /// Gets extended metadata parsed from the already loaded media page
    /// </summary>
    public MediaDetails Details { get; }

    /// <summary>
    /// Gets the internal HDRezka user rating and vote count parsed from the page or returned after <see cref="RateAsync"/>
    /// </summary>
    public Rating Rating { get; private set; }

    /// <summary>
    /// Submits the authenticated user's internal HDRezka rating for this media
    /// </summary>
    /// <param name="value">
    /// Integer rating from one through ten
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel rating submission and response parsing
    /// </param>
    /// <returns>
    /// Updated aggregate HDRezka rating and vote count
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is outside the inclusive range from one through ten
    /// </exception>
    /// <exception cref="RatingException">
    /// Authentication is missing, the account has already voted, or the website rejected the rating
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The rating response did not contain a numeric value and vote count
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The rating endpoint returned malformed JSON
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<Rating> RateAsync(
        int value,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 10);
        var response = await _transport.GetJsonAsync<RatingResponse>(
            new Uri(Origin, "/engine/ajax/rating.php"),
            new Dictionary<string, string?>
            {
                ["news_id"] = Id.ToString(CultureInfo.InvariantCulture),
                ["go_rate"] = value.ToString(CultureInfo.InvariantCulture),
                ["skin"] = "hdrezka"
            },
            cancellationToken).ConfigureAwait(false);
        if (!response.Success)
        {
            throw new RatingException(
                GetString(response.Message) ?? "The website rejected the rating.");
        }

        var ratingValue = TryGetDouble(response.Num);
        var votes = TryGetInt32(response.Votes);
        if (!ratingValue.HasValue || !votes.HasValue)
        {
            throw new ParseException(
                "The rating response has no numeric value or vote count.");
        }

        Rating = new Rating(ratingValue, votes);
        return Rating;
    }

    /// <summary>
    /// Gets the subscription tier detected for the session that loaded this page
    /// </summary>
    public AccountTier AccountTier { get; }

    /// <summary>
    /// Gets whether the page was loaded with an account that has an active Premium subscription
    /// </summary>
    public bool IsPremiumAccount => AccountTier == AccountTier.Premium;

    /// <summary>
    /// Gets current player availability and the reason reported by the website
    /// </summary>
    public PlaybackState Playback { get; }

    /// <summary>
    /// Gets every translation variant in the same order as the website
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="Translators"/>, this list preserves variants that share the same numeric identifier
    /// </remarks>
    public IReadOnlyList<Translator> TranslationOptions { get; }

    /// <summary>
    /// Gets the first available translation variant for every numeric identifier
    /// </summary>
    /// <remarks>
    /// Use <see cref="TranslationOptions"/> when variants with the same identifier must remain distinct
    /// </remarks>
    public IReadOnlyDictionary<int, Translator> Translators { get; }

    /// <summary>
    /// Gets available translators indexed by name without case sensitivity
    /// </summary>
    public IReadOnlyDictionary<string, Translator> TranslatorsByName { get; }

    /// <summary>
    /// Gets other parts related to the current title in the order shown on the page
    /// </summary>
    public IReadOnlyList<RelatedPart> OtherParts { get; }

    /// <summary>
    /// Loads complete media objects for every related part using the shared HTTP transport
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel related page loading and parsing
    /// </param>
    /// <returns>
    /// Loaded media objects in the same order as <see cref="OtherParts"/>
    /// </returns>
    /// <remarks>
    /// The returned objects share this instance's transport and should be disposed after use. Loading is limited by <see cref="ClientOptions.MaxConcurrentRequests"/>
    /// </remarks>
    /// <exception cref="LoginRequiredException">
    /// The website returned its login page
    /// </exception>
    /// <exception cref="CaptchaException">
    /// The website requested captcha verification
    /// </exception>
    /// <exception cref="HttpException">
    /// A related media page returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// Required related media data could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// A related media request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<IReadOnlyList<Media>> GetOtherPartsAsync(
        CancellationToken cancellationToken = default) =>
        await AsyncUtilities.SelectAsync(
            OtherParts,
            _transport.Options.MaxConcurrentRequests,
            (part, token) => Media.LoadAsync(
                _transport,
                ownsTransport: false,
                part.Url,
                token),
            cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Loads trailer metadata through the website trailer endpoint
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel trailer loading and embed parsing
    /// </param>
    /// <returns>
    /// Trailer title, description, player markup, and resolved URLs
    /// </returns>
    /// <exception cref="TrailerException">
    /// The media has no trailer or the website rejected the request
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The trailer response or player markup could not be read
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The trailer endpoint returned malformed JSON
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<Trailer> GetTrailerAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await _transport.PostFormJsonAsync<TrailerResponse>(
            new Uri(Origin, "/engine/ajax/gettrailervideo.php"),
            new Dictionary<string, string>
            {
                ["id"] = Id.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken,
            Url).ConfigureAwait(false);
        if (!response.Success)
        {
            throw new TrailerException(
                GetString(response.Message) ?? "The website did not provide a trailer.");
        }

        var embedHtml = response.Code ?? "";
        if (string.IsNullOrWhiteSpace(embedHtml))
        {
            throw new ParseException("The trailer response has no player markup.");
        }

        var document = await Parsing.ParseDocumentAsync(embedHtml, cancellationToken)
            .ConfigureAwait(false);
        var sourceValue = document.QuerySelector("iframe, video, source")?.GetAttribute("src");
        return new Trailer(
            response.Title?.Trim() ?? "",
            response.Description?.Trim() ?? "",
            embedHtml,
            string.IsNullOrWhiteSpace(sourceValue) ? null : new Uri(Origin, sourceValue),
            string.IsNullOrWhiteSpace(response.Link) ? null : new Uri(Origin, response.Link));
    }

    /// <summary>
    /// Changes the watched state of one release-schedule episode when needed
    /// </summary>
    /// <param name="entry">
    /// Schedule entry loaded from <see cref="MediaDetails.Schedule"/>
    /// </param>
    /// <param name="isWatched">
    /// Desired watched state
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the toggle request
    /// </param>
    /// <returns>
    /// Updated immutable schedule entry
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="entry"/> is <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The schedule identifier is not positive
    /// </exception>
    /// <exception cref="AccountOperationException">
    /// Authentication is missing or the website rejected the change
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The response status could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<EpisodeScheduleEntry> SetScheduleWatchedAsync(
        EpisodeScheduleEntry entry,
        bool isWatched,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentOutOfRangeException.ThrowIfLessThan(entry.Id, 1);
        if (entry.IsWatched == isWatched)
        {
            return entry;
        }

        var response = await _transport.PostFormJsonAsync<MutationResponse>(
            new Uri(Origin, "/engine/ajax/schedule_watched.php"),
            new Dictionary<string, string>
            {
                ["id"] = entry.Id.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken,
            Url).ConfigureAwait(false);
        if (!response.Success)
        {
            throw new AccountOperationException(
                GetString(response.Message) ?? "The schedule watched state could not be changed.");
        }

        return entry with { IsWatched = isWatched };
    }

    /// <summary>
    /// Gets the mutable translator priority list used when no translation is selected explicitly
    /// </summary>
    public IList<int> PreferredTranslators => _transport.Options.PreferredTranslators;

    /// <summary>
    /// Gets the mutable list of translators placed after preferred and neutral choices
    /// </summary>
    public IList<int> NonPreferredTranslators => _transport.Options.NonPreferredTranslators;

    /// <summary>
    /// Loads and parses a media page without creating a reusable <see cref="Client"/>
    /// </summary>
    /// <param name="url">
    /// Absolute movie or series page URL
    /// </param>
    /// <param name="options">
    /// Request and translator settings, or <see langword="null"/> to use the defaults
    /// </param>
    /// <param name="httpClient">
    /// HTTP client used for requests, or <see langword="null"/> to create an internal client.
    /// The supplied client remains owned by the caller
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Loaded media metadata and operations for streams, seasons, and episodes
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> is empty or contains only whitespace
    /// </exception>
    /// <exception cref="UriFormatException">
    /// <paramref name="url"/> is not a valid absolute URI
    /// </exception>
    /// <exception cref="LoginRequiredException">
    /// The website returned its login page
    /// </exception>
    /// <exception cref="CaptchaException">
    /// The website requested captcha verification
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// Required media data could not be read from the page
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public static async Task<Media> CreateAsync(
        string url,
        ClientOptions? options = null,
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        return await CreateAsync(
            new Uri(url, UriKind.Absolute),
            options,
            httpClient,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads and parses a media page without creating a reusable <see cref="Client"/>
    /// </summary>
    /// <param name="url">
    /// Absolute movie or series page URL
    /// </param>
    /// <param name="options">
    /// Request and translator settings, or <see langword="null"/> to use the defaults
    /// </param>
    /// <param name="httpClient">
    /// HTTP client used for requests, or <see langword="null"/> to create an internal client.
    /// The supplied client remains owned by the caller
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Loaded media metadata and operations for streams, seasons, and episodes
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="url"/> is <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> is relative
    /// </exception>
    /// <exception cref="LoginRequiredException">
    /// The website returned its login page
    /// </exception>
    /// <exception cref="CaptchaException">
    /// The website requested captcha verification
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// Required media data could not be read from the page
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public static async Task<Media> CreateAsync(
        Uri url,
        ClientOptions? options = null,
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var transport = new HttpTransport((options ?? new ClientOptions()).Clone(), httpClient);
        try
        {
            return await LoadAsync(transport, ownsTransport: true, url, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            transport.Dispose();
            throw;
        }
    }

    internal static async Task<Media> LoadAsync(
        HttpTransport transport,
        bool ownsTransport,
        Uri url,
        CancellationToken cancellationToken)
    {
        var normalizedUrl = NormalizeContentUri(url);
        var html = await transport.GetStringAsync(normalizedUrl, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        IScraper scraper = new Scraper();
        var page = await scraper.ParseMediaPageAsync(html, normalizedUrl, cancellationToken)
            .ConfigureAwait(false);
        return new Media(transport, ownsTransport, normalizedUrl, page, scraper);
    }

    /// <summary>
    /// Orders translators by preferred choices, neutral choices, and non-preferred choices
    /// </summary>
    /// <param name="translators">
    /// Translators to order, or <see langword="null"/> to use <see cref="TranslationOptions"/>
    /// </param>
    /// <param name="preferred">
    /// Translator identifiers placed first in the supplied order, or <see langword="null"/> to use <see cref="PreferredTranslators"/>
    /// </param>
    /// <param name="nonPreferred">
    /// Translator identifiers placed last in the supplied order, or <see langword="null"/> to use <see cref="NonPreferredTranslators"/>
    /// </param>
    /// <returns>
    /// New ordered list that keeps the original order between translators with equal priority
    /// </returns>
    public IReadOnlyList<Translator> SortTranslators(
        IEnumerable<Translator>? translators = null,
        IEnumerable<int>? preferred = null,
        IEnumerable<int>? nonPreferred = null)
    {
        return TranslatorSelector.Sort(
            translators ?? TranslationOptions,
            preferred ?? PreferredTranslators,
            nonPreferred ?? NonPreferredTranslators);
    }

    /// <summary>
    /// Loads and caches season and episode identifiers separately for every translator
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel waiting for the cached or active load
    /// </param>
    /// <returns>
    /// Series information indexed by translator identifier
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The loaded page is not a series
    /// </exception>
    /// <exception cref="PlaybackUnavailableException">
    /// The media page exists but the website does not currently provide a player
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status after transient retry handling
    /// </exception>
    /// <exception cref="ParseException">
    /// Episode data or the JSON response could not be read
    /// </exception>
    /// <exception cref="JsonException">
    /// The episode endpoint returned malformed JSON
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Waiting for the result was canceled
    /// </exception>
    public Task<IReadOnlyDictionary<int, SeriesInfo>> GetSeriesInfoAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureSeries();
        ThrowIfPlaybackUnavailable();
        lock (_cacheLock)
        {
            _seriesInfoTask ??= LoadSeriesInfoAsync();
            return _seriesInfoTask.WaitAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Loads and caches season and episode identifiers for one translator
    /// </summary>
    /// <param name="translation">
    /// Translator identifier or exact name
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel waiting for the cached or active load
    /// </param>
    /// <returns>
    /// Season and episode information for the requested translator
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="translation"/> is <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="translation"/> is empty or does not match an available translator
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The loaded page is not a series
    /// </exception>
    /// <exception cref="StreamFetchException">
    /// The website did not return season information for the requested translator
    /// </exception>
    /// <exception cref="PlaybackUnavailableException">
    /// The media page exists but the website does not currently provide a player
    /// </exception>
    /// <exception cref="PremiumRequiredException">
    /// The requested translation requires an active Premium subscription
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status after transient retry handling
    /// </exception>
    /// <exception cref="ParseException">
    /// Episode data or the JSON response could not be read
    /// </exception>
    /// <exception cref="JsonException">
    /// The episode endpoint returned malformed JSON
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Waiting for the result was canceled
    /// </exception>
    public async Task<SeriesInfo> GetSeriesInfoAsync(
        string translation,
        CancellationToken cancellationToken = default)
    {
        EnsureSeries();
        ArgumentException.ThrowIfNullOrWhiteSpace(translation);
        ThrowIfPlaybackUnavailable();
        var translator = GetTranslatorCandidates(
            translation,
            preferred: null,
            nonPreferred: null)[0];
        return await GetSeriesInfoForTranslatorAsync(
            translator,
            cancellationToken).ConfigureAwait(false)
            ?? throw new StreamFetchException();
    }

    /// <summary>
    /// Loads and caches seasons and episodes with translations merged across all translators
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel waiting for the cached or active load
    /// </param>
    /// <returns>
    /// Seasons ordered by number with their episodes and available translations
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The loaded page is not a series
    /// </exception>
    /// <exception cref="PlaybackUnavailableException">
    /// The media page exists but the website does not currently provide a player
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status after transient retry handling
    /// </exception>
    /// <exception cref="ParseException">
    /// Episode data or the JSON response could not be read
    /// </exception>
    /// <exception cref="JsonException">
    /// The episode endpoint returned malformed JSON
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Waiting for the result was canceled
    /// </exception>
    public Task<IReadOnlyList<Season>> GetEpisodesInfoAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureSeries();
        ThrowIfPlaybackUnavailable();
        lock (_cacheLock)
        {
            _episodesInfoTask ??= LoadEpisodesInfoAsync();
            return _episodesInfoTask.WaitAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Adds this media to a bookmark folder or removes it when requested
    /// </summary>
    /// <param name="folderId">
    /// Numeric bookmark folder identifier
    /// </param>
    /// <param name="isBookmarked">
    /// <see langword="true"/> to add the media or <see langword="false"/> to remove it
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the request
    /// </param>
    /// <remarks>
    /// No request is sent when the loaded media page already has the requested state
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="folderId"/> is not positive
    /// </exception>
    /// <exception cref="AccountOperationException">
    /// The website rejected the bookmark change
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The response could not be read
    /// </exception>
    /// <exception cref="JsonException">
    /// The bookmark endpoint returned malformed JSON
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task SetBookmarkAsync(
        long folderId,
        bool isBookmarked,
        CancellationToken cancellationToken = default)
    {
        if (folderId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(folderId));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _bookmarkMutationLock
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            lock (_bookmarkLock)
            {
                if (_bookmarkFolderIds.Contains(folderId) == isBookmarked)
                {
                    return;
                }
            }

            await new AccountClient(_transport, Origin)
                .ToggleBookmarkAsync(Id, folderId, cancellationToken)
                .ConfigureAwait(false);
            lock (_bookmarkLock)
            {
                if (isBookmarked)
                {
                    _bookmarkFolderIds.Add(folderId);
                }
                else
                {
                    _bookmarkFolderIds.Remove(folderId);
                }
            }
        }
        finally
        {
            _bookmarkMutationLock.Release();
        }
    }

    /// <summary>
    /// Loads a movie stream or one episode stream for a series
    /// </summary>
    /// <param name="season">
    /// Season number required for a series and ignored for a movie
    /// </param>
    /// <param name="episode">
    /// Episode number required for a series and ignored for a movie
    /// </param>
    /// <param name="translation">
    /// Translator identifier or exact name, or <see langword="null"/> to select by priority
    /// </param>
    /// <param name="preferred">
    /// Translator identifiers preferred for this call, or <see langword="null"/> to use <see cref="PreferredTranslators"/>
    /// </param>
    /// <param name="nonPreferred">
    /// Translator identifiers placed last for this call, or <see langword="null"/> to use <see cref="NonPreferredTranslators"/>
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel episode discovery or stream loading
    /// </param>
    /// <returns>
    /// Video URLs grouped by quality together with translator and subtitle information
    /// </returns>
    /// <exception cref="ArgumentException">
    /// A series call has no season or episode, the requested season or episode does not exist, or the translation is unavailable
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The page has an unknown media format
    /// </exception>
    /// <exception cref="StreamFetchException">
    /// No translator is available or the website did not return a usable stream
    /// </exception>
    /// <exception cref="PlaybackUnavailableException">
    /// The media page exists but the website does not currently provide a player
    /// </exception>
    /// <exception cref="PremiumRequiredException">
    /// The explicitly selected translation or returned content requires an active Premium subscription
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// Episode data, JSON, stream URLs, subtitles, or response data could not be read
    /// </exception>
    /// <exception cref="JsonException">
    /// The episode or stream endpoint returned malformed JSON
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<MediaStream> GetStreamAsync(
        int? season = null,
        int? episode = null,
        string? translation = null,
        IEnumerable<int>? preferred = null,
        IEnumerable<int>? nonPreferred = null,
        CancellationToken cancellationToken = default)
    {
        if (Format == MediaFormat.Series)
        {
            if (season is null || episode is null)
            {
                throw new ArgumentException("Both season and episode are required for a TV series.");
            }

            ThrowIfPlaybackUnavailable();
            var (translator, _) = await FindSeriesTranslatorAsync(
                season.Value,
                episode.Value,
                translation,
                preferred,
                nonPreferred,
                cancellationToken).ConfigureAwait(false);
            return await FetchStreamAsync(
                season,
                episode,
                translator,
                "get_stream",
                cancellationToken).ConfigureAwait(false);
        }

        if (Format == MediaFormat.Movie)
        {
            ThrowIfPlaybackUnavailable();
            var translator = GetTranslatorCandidates(
                translation,
                preferred,
                nonPreferred)[0];
            return await FetchStreamAsync(
                null,
                null,
                translator,
                "get_movie",
                cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException("The page has an unknown content format.");
    }

    /// <summary>
    /// Loads every available episode stream from one season using the same translator
    /// </summary>
    /// <param name="season">
    /// Season number to load
    /// </param>
    /// <param name="translation">
    /// Translator identifier or exact name, or <see langword="null"/> to select by priority
    /// </param>
    /// <param name="ignoreErrors">
    /// <see langword="false"/> to store <see langword="null"/> after two failed attempts per episode.
    /// <see langword="true"/> to keep retrying each episode until it succeeds or cancellation is requested
    /// </param>
    /// <param name="progress">
    /// Optional receiver notified before loading starts and after each completed episode
    /// </param>
    /// <param name="preferred">
    /// Translator identifiers preferred for this call, or <see langword="null"/> to use <see cref="PreferredTranslators"/>
    /// </param>
    /// <param name="nonPreferred">
    /// Translator identifiers placed last for this call, or <see langword="null"/> to use <see cref="NonPreferredTranslators"/>
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel episode discovery, stream loading, or retry delays
    /// </param>
    /// <returns>
    /// Episode numbers mapped to loaded streams, with <see langword="null"/> for episodes that still failed after retrying
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The season does not exist or the selected translation is unavailable for that season
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The loaded page is not a series
    /// </exception>
    /// <exception cref="StreamFetchException">
    /// No translator is available before episode loading begins
    /// </exception>
    /// <exception cref="PlaybackUnavailableException">
    /// The media page exists but the website does not currently provide a player
    /// </exception>
    /// <exception cref="PremiumRequiredException">
    /// The explicitly selected translation or returned content requires an active Premium subscription
    /// </exception>
    /// <exception cref="HttpException">
    /// Loading the season metadata returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// Season or episode metadata could not be read
    /// </exception>
    /// <exception cref="JsonException">
    /// The episode endpoint returned malformed JSON while loading season metadata
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// Loading the season metadata could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<IReadOnlyDictionary<int, MediaStream?>> GetSeasonStreamsAsync(
        int season,
        string? translation = null,
        bool ignoreErrors = false,
        IProgress<SeasonDownloadProgress>? progress = null,
        IEnumerable<int>? preferred = null,
        IEnumerable<int>? nonPreferred = null,
        CancellationToken cancellationToken = default)
    {
        EnsureSeries();
        ThrowIfPlaybackUnavailable();
        var (translator, seriesInfo) = await FindSeriesTranslatorAsync(
            season,
            null,
            translation,
            preferred,
            nonPreferred,
            cancellationToken).ConfigureAwait(false);
        var episodeNumbers = seriesInfo.Episodes[season].Keys.Order().ToList();
        progress?.Report(new SeasonDownloadProgress(0, episodeNumbers.Count));
        var completed = 0;
        var loaded = await AsyncUtilities.SelectAsync(
            episodeNumbers,
            _transport.Options.MaxConcurrentRequests,
            async (episode, token) =>
            {
                var stream = await LoadEpisodeStreamWithRetryAsync(
                    season,
                    episode,
                    translator,
                    ignoreErrors,
                    token).ConfigureAwait(false);
                progress?.Report(
                    new SeasonDownloadProgress(
                        Interlocked.Increment(ref completed),
                        episodeNumbers.Count));
                return KeyValuePair.Create(episode, stream);
            },
            cancellationToken).ConfigureAwait(false);
        return new SortedDictionary<int, MediaStream?>(
            loaded.ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    private async Task<IReadOnlyDictionary<int, SeriesInfo>> LoadSeriesInfoAsync()
    {
        var result = new Dictionary<int, SeriesInfo>();
        var translators = GetTranslatorCandidates(
            translation: null,
            preferred: null,
            nonPreferred: null);
        var information = await AsyncUtilities.SelectAsync(
            translators,
            _transport.Options.MaxConcurrentRequests,
            (translator, _) => GetSeriesInfoForTranslatorAsync(
                translator,
                CancellationToken.None)).ConfigureAwait(false);
        for (var index = 0; index < translators.Count; index++)
        {
            var info = information[index];
            if (info is not null)
            {
                result.TryAdd(translators[index].Id, info);
            }
        }

        return result;
    }

    private async Task<MediaStream?> LoadEpisodeStreamWithRetryAsync(
        int season,
        int episode,
        Translator translator,
        bool ignoreErrors,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await FetchStreamAsync(
                    season,
                    episode,
                    translator,
                    "get_stream",
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (PremiumRequiredException)
            {
                throw;
            }
            catch (Exception) when (attempt++ == 0 || ignoreErrors)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    private async Task<SeriesInfo?> GetSeriesInfoForTranslatorAsync(
        Translator translator,
        CancellationToken cancellationToken)
    {
        Task<SeriesInfo?> task;
        var key = CreateTranslatorKey(translator);
        lock (_cacheLock)
        {
            if (!_seriesInfoByTranslator.TryGetValue(key, out task!))
            {
                task = LoadSeriesInfoForTranslatorAsync(translator);
                _seriesInfoByTranslator[key] = task;
            }
        }

        try
        {
            return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            lock (_cacheLock)
            {
                if (_seriesInfoByTranslator.TryGetValue(key, out var cached) &&
                    ReferenceEquals(cached, task) &&
                    task.IsCompleted)
                {
                    _seriesInfoByTranslator.Remove(key);
                }
            }

            throw;
        }
    }

    private async Task<SeriesInfo?> LoadSeriesInfoForTranslatorAsync(Translator translator)
    {
        PlayerResponse response;
        var attempt = 0;
        while (true)
        {
            try
            {
                response = await _transport.PostFormJsonAsync<PlayerResponse>(
                    CreatePlayerUri(),
                    CreatePlayerData(translator, "get_episodes"))
                    .ConfigureAwait(false);
                break;
            }
            catch (HttpException exception)
                when (attempt++ == 0 && IsTransient(exception))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
            }
        }

        ThrowIfPremiumResponse(response, translator);
        if (!response.Success)
        {
            return null;
        }

        var snapshot = _scraper.ParseEpisodes(
            response.Seasons ?? "",
            response.Episodes ?? "");
        var result = new SeriesInfo(
            translator.Id,
            translator.Name,
            translator.IsPremium,
            snapshot.Seasons,
            snapshot.Episodes);

        var payload = GetString(response.Url);
        if (!string.IsNullOrWhiteSpace(payload) &&
            snapshot.SelectedSeason.HasValue &&
            snapshot.SelectedEpisode.HasValue)
        {
            try
            {
                var stream = ParseStreamResponse(
                    response,
                    snapshot.SelectedSeason,
                    snapshot.SelectedEpisode,
                    translator);
                CacheStream(stream, translator);
            }
            catch (ParseException exception)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "The optional initial stream could not be cached: {0}",
                    exception.Message);
            }
        }

        return result;
    }

    private static bool IsTransient(HttpException exception) =>
        (int)exception.StatusCode is 429 or 502 or 503 or 504;

    private async Task<IReadOnlyList<Season>> LoadEpisodesInfoAsync()
    {
        var byTranslator = await GetSeriesInfoAsync().ConfigureAwait(false);
        var seasons = new SortedDictionary<int, (string Title,
            SortedDictionary<int, (string Title, List<EpisodeTranslation> Translations)> Episodes)>();

        foreach (var info in byTranslator.Values)
        {
            foreach (var season in info.Seasons)
            {
                if (!seasons.TryGetValue(season.Key, out var seasonData))
                {
                    seasonData = (season.Value, []);
                    seasons[season.Key] = seasonData;
                }

                if (!info.Episodes.TryGetValue(season.Key, out var episodes))
                {
                    continue;
                }

                foreach (var episode in episodes)
                {
                    if (!seasonData.Episodes.TryGetValue(episode.Key, out var episodeData))
                    {
                        episodeData = (episode.Value, []);
                        seasonData.Episodes[episode.Key] = episodeData;
                    }

                    episodeData.Translations.Add(
                        new EpisodeTranslation(
                            info.TranslatorId,
                            info.TranslatorName,
                            info.IsPremium));
                }
            }
        }

        return seasons
            .Select(season => new Season(
                season.Key,
                season.Value.Title,
                season.Value.Episodes
                    .Select(episode => new Episode(
                        episode.Key,
                        episode.Value.Title,
                        episode.Value.Translations))
                    .ToList()))
            .ToList();
    }

    private async Task<MediaStream> FetchStreamAsync(
        int? season,
        int? episode,
        Translator translator,
        string action,
        CancellationToken cancellationToken)
    {
        ThrowIfPremiumTranslationUnavailable(translator);
        var key = new StreamKey(CreateTranslatorKey(translator), season, episode);
        lock (_cacheLock)
        {
            if (_streamCache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        var response = await _transport.PostFormJsonAsync<PlayerResponse>(
            CreatePlayerUri(),
            CreatePlayerData(translator, action, season, episode),
            cancellationToken).ConfigureAwait(false);

        ThrowIfPremiumResponse(response, translator);
        if (!response.Success || string.IsNullOrWhiteSpace(GetString(response.Url)))
        {
            throw new StreamFetchException();
        }

        var stream = ParseStreamResponse(response, season, episode, translator);
        CacheStream(stream, translator);
        return stream;
    }

    private async Task<(Translator Translator, SeriesInfo Info)> FindSeriesTranslatorAsync(
        int season,
        int? episode,
        string? translation,
        IEnumerable<int>? preferred,
        IEnumerable<int>? nonPreferred,
        CancellationToken cancellationToken)
    {
        var candidates = GetTranslatorCandidates(
            translation,
            preferred,
            nonPreferred);
        if (candidates.Count == 0)
        {
            throw new StreamFetchException();
        }

        var seasonFound = false;
        foreach (var translator in candidates)
        {
            var info = await GetSeriesInfoForTranslatorAsync(
                translator,
                cancellationToken).ConfigureAwait(false);
            if (info is null ||
                !info.Episodes.TryGetValue(season, out var episodes))
            {
                continue;
            }

            seasonFound = true;
            if (!episode.HasValue || episodes.ContainsKey(episode.Value))
            {
                return (translator, info);
            }
        }

        if (!seasonFound)
        {
            if (!string.IsNullOrWhiteSpace(translation))
            {
                throw new ArgumentException(
                    $"Translation \"{translation}\" does not provide season \"{season}\".",
                    nameof(translation));
            }

            throw new ArgumentException($"Season \"{season}\" was not found.", nameof(season));
        }

        if (!string.IsNullOrWhiteSpace(translation))
        {
            throw new ArgumentException(
                $"Translation \"{translation}\" does not provide episode \"{episode}\" " +
                $"in season \"{season}\".",
                nameof(translation));
        }

        throw new ArgumentException(
            $"Episode \"{episode}\" in season \"{season}\" was not found.",
            nameof(episode));
    }

    private IReadOnlyList<Translator> GetTranslatorCandidates(
        string? translation,
        IEnumerable<int>? preferred,
        IEnumerable<int>? nonPreferred)
    {
        if (string.IsNullOrWhiteSpace(translation))
        {
            var available = AccountTier == AccountTier.Standard
                ? TranslationOptions.Where(translator => !translator.IsPremium)
                : TranslationOptions;
            var sorted = SortTranslators(
                available,
                preferred: preferred,
                nonPreferred: nonPreferred);
            if (sorted.Count == 0)
            {
                if (AccountTier == AccountTier.Standard &&
                    TranslationOptions.Any(translator => translator.IsPremium))
                {
                    throw new PremiumRequiredException(PremiumFeature.Translation);
                }

                throw new StreamFetchException();
            }

            return sorted;
        }

        IReadOnlyList<Translator> matches;
        if (int.TryParse(
            translation,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var translatorId))
        {
            matches = TranslationOptions
                .Where(translator => translator.Id == translatorId)
                .ToList();
        }
        else
        {
            matches = TranslationOptions
                .Where(translator =>
                    translator.Name.Equals(translation, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (matches.Count == 0)
        {
            throw new ArgumentException(
                $"Translation \"{translation}\" is not defined.",
                nameof(translation));
        }

        if (AccountTier == AccountTier.Standard)
        {
            var accessible = matches.Where(translator => !translator.IsPremium).ToList();
            if (accessible.Count == 0)
            {
                throw new PremiumRequiredException(
                    PremiumFeature.Translation,
                    matches[0].Name);
            }

            matches = accessible;
        }

        return SortTranslators(matches, preferred, nonPreferred);
    }

    private Dictionary<string, string> CreatePlayerData(
        Translator translator,
        string action,
        int? season = null,
        int? episode = null)
    {
        var data = new Dictionary<string, string>
        {
            ["id"] = Id.ToString(CultureInfo.InvariantCulture),
            ["translator_id"] = translator.Id.ToString(CultureInfo.InvariantCulture),
            ["favs"] = _favorites,
            ["action"] = action
        };
        if (season.HasValue)
        {
            data["season"] = season.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (episode.HasValue)
        {
            data["episode"] = episode.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (action == "get_movie")
        {
            data["is_camrip"] = translator.IsCamrip ? "1" : "0";
            data["is_ads"] = translator.HasAds ? "1" : "0";
            data["is_director"] = translator.IsDirectorCut ? "1" : "0";
        }

        return data;
    }

    private Uri CreatePlayerUri() =>
        new(
            Origin,
            $"/ajax/get_cdn_series/?t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}");

    private MediaStream ParseStreamResponse(
        PlayerResponse response,
        int? season,
        int? episode,
        Translator translator)
    {
        ThrowIfPremiumResponse(response, translator);
        var payload = GetString(response.Url) ??
            throw new StreamFetchException();
        var subtitleLanguages = response.SubtitleLanguages.ValueKind == JsonValueKind.Object
            ? response.SubtitleLanguages.Deserialize<IReadOnlyDictionary<string, string>>()
            : null;
        var thumbnailValue = GetString(response.Thumbnails);
        var thumbnailPreview =
            !string.IsNullOrWhiteSpace(thumbnailValue) &&
            Uri.TryCreate(Origin, thumbnailValue, out var parsedThumbnail)
                ? parsedThumbnail
                : null;
        var snapshot = new StreamSnapshot(
            payload,
            GetString(response.Subtitle),
            subtitleLanguages,
            GetString(response.Quality),
            GetString(response.DefaultSubtitle),
            thumbnailPreview,
            GetBoolean(response.PremiumContent),
            AccountTier);
        return _scraper.ParseStream(
            snapshot,
            season,
            episode,
            Name,
            translator.Id);
    }

    private void CacheStream(MediaStream stream, Translator translator)
    {
        lock (_cacheLock)
        {
            _streamCache[new StreamKey(
                CreateTranslatorKey(translator),
                stream.Season,
                stream.Episode)] = stream;
        }
    }

    private static string? GetString(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            _ => null
        };

    private static bool GetBoolean(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => element.TryGetInt32(out var value) && value != 0,
            JsonValueKind.String =>
                element.GetString() is "1" ||
                bool.TryParse(element.GetString(), out var value) && value,
            _ => false
        };

    private static double? TryGetDouble(JsonElement element)
    {
        var value = GetString(element);
        return double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var result)
                ? result
                : null;
    }

    private static int? TryGetInt32(JsonElement element)
    {
        var value = GetString(element);
        return int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var result)
                ? result
                : null;
    }

    private void ThrowIfPremiumTranslationUnavailable(Translator translator)
    {
        if (translator.IsPremium && AccountTier == AccountTier.Standard)
        {
            throw new PremiumRequiredException(PremiumFeature.Translation, translator.Name);
        }
    }

    private static void ThrowIfPremiumResponse(
        PlayerResponse response,
        Translator translator)
    {
        if (GetBoolean(response.PremiumContent))
        {
            throw new PremiumRequiredException(
                translator.IsPremium ? PremiumFeature.Translation : PremiumFeature.Content,
                translator.IsPremium ? translator.Name : null);
        }
    }

    private static TranslatorKey CreateTranslatorKey(Translator translator) =>
        new(
            translator.Id,
            translator.IsCamrip,
            translator.HasAds,
            translator.IsDirectorCut);

    private void EnsureSeries()
    {
        if (Format != MediaFormat.Series)
        {
            throw new InvalidOperationException(
                "Season and episode information is only available for a TV series.");
        }
    }

    private void ThrowIfPlaybackUnavailable()
    {
        if (!Playback.IsAvailable)
        {
            throw new PlaybackUnavailableException(Playback);
        }
    }

    private static Uri NormalizeContentUri(Uri url)
    {
        if (!url.IsAbsoluteUri)
        {
            throw new ArgumentException("An absolute URL is required.", nameof(url));
        }

        var value = url.AbsoluteUri;
        var end = value.IndexOf(".html", StringComparison.OrdinalIgnoreCase);
        return end >= 0 ? new Uri(value[..(end + 5)]) : url;
    }

    /// <summary>
    /// Returns a readable representation containing the primary media title
    /// </summary>
    /// <returns>
    /// Text in the form <c>Media("Title")</c>
    /// </returns>
    public override string ToString() => $"Media(\"{Name}\")";

    /// <summary>
    /// Releases the internally created HTTP client while leaving a supplied or shared <see cref="HttpClient"/> untouched
    /// </summary>
    public void Dispose()
    {
        _bookmarkMutationLock.Dispose();
        if (_ownsTransport)
        {
            _transport.Dispose();
        }
    }

    private sealed record PlayerResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("seasons")] string? Seasons,
        [property: JsonPropertyName("episodes")] string? Episodes,
        [property: JsonPropertyName("url")] JsonElement Url,
        [property: JsonPropertyName("quality")] JsonElement Quality,
        [property: JsonPropertyName("subtitle")] JsonElement Subtitle,
        [property: JsonPropertyName("subtitle_lns")] JsonElement SubtitleLanguages,
        [property: JsonPropertyName("subtitle_def")] JsonElement DefaultSubtitle,
        [property: JsonPropertyName("thumbnails")] JsonElement Thumbnails,
        [property: JsonPropertyName("premium_content")] JsonElement PremiumContent);

    private sealed record RatingResponse(
        bool Success,
        JsonElement Num,
        JsonElement Votes,
        JsonElement Message);

    private sealed record TrailerResponse(
        bool Success,
        string? Title,
        string? Description,
        string? Code,
        string? Link,
        JsonElement Message);

    private sealed record MutationResponse(
        bool Success,
        JsonElement Message);

    private readonly record struct TranslatorKey(
        int Id,
        bool IsCamrip,
        bool HasAds,
        bool IsDirectorCut);

    private readonly record struct StreamKey(
        TranslatorKey Translator,
        int? Season,
        int? Episode);
}
