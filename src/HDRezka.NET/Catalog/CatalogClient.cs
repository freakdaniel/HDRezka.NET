using HdRezka.Http;
using HdRezka.Scraping;

namespace HdRezka;

/// <summary>
/// Loads paginated media sections shown on the website home page
/// </summary>
public sealed class CatalogClient
{
    private readonly HttpTransport _transport;
    private readonly Uri _origin;

    internal CatalogClient(HttpTransport transport, Uri origin)
    {
        _transport = transport;
        _origin = origin;
    }

    /// <summary>
    /// Loads one page from a category, genre, year, or best-rating directory
    /// </summary>
    /// <param name="query">
    /// Directory filters used to build the website path
    /// </param>
    /// <param name="page">
    /// One-based page number
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Media cards and pagination information for the requested directory
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="query"/> is <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The category is unsupported or the genre contains characters that cannot be used in a website slug
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="page"/> is less than one or the supplied year is outside the website directory range
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
    /// A catalog card or response page could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task<PageResult<CatalogItem>> GetDirectoryAsync(
        CatalogQuery query,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        if (query.Year is < 1890 or > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "The release year must be between 1890 and 2100.");
        }

        var category = query.Category switch
        {
            MediaCategory.Film => "films",
            MediaCategory.Series => "series",
            MediaCategory.Cartoon => "cartoons",
            MediaCategory.Anime => "animation",
            MediaCategory.Show => "show",
            _ => throw new ArgumentException("The website does not expose a directory for this category.", nameof(query))
        };
        var genre = query.Genre?.Trim().Trim('/');
        if (!string.IsNullOrEmpty(genre) &&
            genre.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '-')))
        {
            throw new ArgumentException("The genre must be a website slug.", nameof(query));
        }

        var segments = new List<string> { category };
        if (query.Best)
        {
            segments.Add("best");
        }

        if (!string.IsNullOrEmpty(genre))
        {
            segments.Add(genre);
        }

        if (query.Year.HasValue)
        {
            if (!query.Best)
            {
                throw new ArgumentException("Year filtering is available only for best-rating directories.", nameof(query));
            }

            segments.Add(query.Year.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        var path = $"/{string.Join('/', segments)}/";
        return LoadListingAsync(path, page, query: null, cancellationToken);
    }

    /// <summary>
    /// Loads a catalog-compatible website path without requiring a predefined directory model
    /// </summary>
    /// <param name="path">
    /// Relative path on the configured website such as <c>/films/country/usa/</c>
    /// </param>
    /// <param name="page">
    /// One-based page number appended to the supplied path
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Media cards and pagination information for the requested path
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="path"/> is empty, absolute, or leaves the configured website origin
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="page"/> is less than one
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
    /// A catalog card or response page could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task<PageResult<CatalogItem>> GetDirectoryAsync(
        string path,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        if (Uri.TryCreate(path, UriKind.Absolute, out _))
        {
            throw new ArgumentException("The catalog path must be relative.", nameof(path));
        }

        var normalized = $"/{path.Trim('/')}/";
        if (normalized.Contains("/../", StringComparison.Ordinal) || normalized.Contains("/./", StringComparison.Ordinal))
        {
            throw new ArgumentException("The catalog path cannot contain relative traversal segments.", nameof(path));
        }

        return LoadListingAsync(normalized, page, query: null, cancellationToken);
    }

    /// <summary>
    /// Loads one page from a home-page catalog section
    /// </summary>
    /// <param name="section">
    /// Section to load
    /// </param>
    /// <param name="category">
    /// Media category to include, or <see cref="MediaCategory.Unknown"/> to include every category
    /// </param>
    /// <param name="page">
    /// One-based page number
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Media cards and pagination information for the requested section
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="page"/> is less than one or <paramref name="category"/> is not supported by the website filter
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
    /// A catalog card or response page could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<PageResult<CatalogItem>> GetPageAsync(
        CatalogSection section,
        MediaCategory category = MediaCategory.Unknown,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        var genre = GetGenre(category);
        var path = page == 1 ? "/" : $"/page/{page}/";
        var html = await _transport.GetSharedStringAsync(
            new Uri(_origin, path),
            new Dictionary<string, string?>
            {
                ["filter"] = GetFilter(section),
                ["genre"] = genre
            },
            cancellationToken).ConfigureAwait(false);
        return await CatalogParser.ParsePageAsync(
            html,
            _origin,
            page,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads recently added media
    /// </summary>
    /// <param name="category">
    /// Media category to include, or <see cref="MediaCategory.Unknown"/> to include every category
    /// </param>
    /// <param name="page">
    /// One-based page number
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Recently added media cards and pagination information
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="page"/> is less than one or <paramref name="category"/> is not supported by the website filter
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
    /// A catalog card or response page could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task<PageResult<CatalogItem>> GetLatestAsync(
        MediaCategory category = MediaCategory.Unknown,
        int page = 1,
        CancellationToken cancellationToken = default) =>
        GetPageAsync(CatalogSection.Latest, category, page, cancellationToken);

    /// <summary>
    /// Loads media currently popular with website users
    /// </summary>
    /// <param name="category">
    /// Media category to include, or <see cref="MediaCategory.Unknown"/> to include every category
    /// </param>
    /// <param name="page">
    /// One-based page number
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Popular media cards and pagination information
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="page"/> is less than one or <paramref name="category"/> is not supported by the website filter
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
    /// A catalog card or response page could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task<PageResult<CatalogItem>> GetPopularAsync(
        MediaCategory category = MediaCategory.Unknown,
        int page = 1,
        CancellationToken cancellationToken = default) =>
        GetPageAsync(CatalogSection.Popular, category, page, cancellationToken);

    /// <summary>
    /// Loads announced media that has not been released yet
    /// </summary>
    /// <param name="category">
    /// Media category to include, or <see cref="MediaCategory.Unknown"/> to include every category
    /// </param>
    /// <param name="page">
    /// One-based page number
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Upcoming media cards and pagination information
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="page"/> is less than one or <paramref name="category"/> is not supported by the website filter
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
    /// A catalog card or response page could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task<PageResult<CatalogItem>> GetUpcomingAsync(
        MediaCategory category = MediaCategory.Unknown,
        int page = 1,
        CancellationToken cancellationToken = default) =>
        GetPageAsync(CatalogSection.Upcoming, category, page, cancellationToken);

    /// <summary>
    /// Loads media being watched by website users right now
    /// </summary>
    /// <param name="category">
    /// Media category to include, or <see cref="MediaCategory.Unknown"/> to include every category
    /// </param>
    /// <param name="page">
    /// One-based page number
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Currently watched media cards and pagination information
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="page"/> is less than one or <paramref name="category"/> is not supported by the website filter
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
    /// A catalog card or response page could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task<PageResult<CatalogItem>> GetWatchingAsync(
        MediaCategory category = MediaCategory.Unknown,
        int page = 1,
        CancellationToken cancellationToken = default) =>
        GetPageAsync(CatalogSection.Watching, category, page, cancellationToken);

    /// <summary>
    /// Loads media from the dedicated new-releases directory
    /// </summary>
    /// <param name="category">
    /// Media category to include, or <see cref="MediaCategory.Unknown"/> to include every category
    /// </param>
    /// <param name="page">
    /// One-based page number
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// New-release media cards and pagination information
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="page"/> is less than one or <paramref name="category"/> is not supported by the website filter
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
    /// A catalog card or response page could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task<PageResult<CatalogItem>> GetNewReleasesAsync(
        MediaCategory category = MediaCategory.Unknown,
        int page = 1,
        CancellationToken cancellationToken = default) =>
        LoadListingAsync(
            "/new/",
            page,
            new Dictionary<string, string?>
            {
                ["filter"] = "last",
                ["genre"] = GetGenre(category)
            },
            cancellationToken);

    /// <summary>
    /// Loads media from the trailer and announcement directory
    /// </summary>
    /// <param name="page">
    /// One-based page number
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Announced media cards and pagination information
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="page"/> is less than one
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
    /// A catalog card or response page could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task<PageResult<CatalogItem>> GetAnnouncementsAsync(
        int page = 1,
        CancellationToken cancellationToken = default) =>
        LoadListingAsync(
            "/announce/",
            page,
            query: null,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Loads television programs and entertainment shows
    /// </summary>
    /// <param name="page">
    /// One-based page number
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Show cards and pagination information
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="page"/> is less than one
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
    /// A catalog card or response page could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task<PageResult<CatalogItem>> GetShowsAsync(
        int page = 1,
        CancellationToken cancellationToken = default) =>
        LoadListingAsync(
            "/show/",
            page,
            query: null,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Loads the compact newest-media slider shown on the website home page
    /// </summary>
    /// <param name="category">
    /// Media category to include, or <see cref="MediaCategory.Unknown"/> to include every supported category
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel waiting for the shared request and response parsing
    /// </param>
    /// <returns>
    /// Compact media cards in website order
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="category"/> is not supported by the slider endpoint
    /// </exception>
    public async Task<IReadOnlyList<CatalogItem>> GetNewestSliderAsync(
        MediaCategory category = MediaCategory.Unknown,
        CancellationToken cancellationToken = default)
    {
        var categoryId = category switch
        {
            MediaCategory.Unknown => 0,
            MediaCategory.Film => 1,
            MediaCategory.Series => 2,
            MediaCategory.Cartoon => 3,
            MediaCategory.Anime => 82,
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };
        var html = await _transport.PostSharedFormAsync(
            new Uri(_origin, "/engine/ajax/get_newest_slider_content.php"),
            new Dictionary<string, string>
            {
                ["id"] = categoryId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            },
            cancellationToken,
            _origin).ConfigureAwait(false);
        return await CatalogParser.ParseItemsAsync(
            html,
            _origin,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads compact hover-preview metadata for one catalog item
    /// </summary>
    /// <param name="item">
    /// Catalog item containing the numeric media identifier
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel waiting for the shared request and response parsing
    /// </param>
    /// <returns>
    /// Compact media metadata returned by the website
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="item"/> is <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="item"/> does not expose a numeric media identifier
    /// </exception>
    public Task<QuickContent> GetQuickContentAsync(
        CatalogItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!item.Id.HasValue)
        {
            throw new ArgumentException(
                "The catalog item must expose a numeric media identifier.",
                nameof(item));
        }

        return GetQuickContentAsync(item.Id.Value, cancellationToken);
    }

    /// <summary>
    /// Loads compact hover-preview metadata for a media identifier
    /// </summary>
    /// <param name="mediaId">
    /// Positive numeric media identifier
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel waiting for the shared request and response parsing
    /// </param>
    /// <returns>
    /// Compact media metadata returned by the website
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="mediaId"/> is not positive
    /// </exception>
    public async Task<QuickContent> GetQuickContentAsync(
        int mediaId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(mediaId, 1);
        var html = await _transport.PostSharedFormAsync(
            new Uri(_origin, "/engine/ajax/quick_content.php"),
            new Dictionary<string, string>
            {
                ["id"] = mediaId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["is_touch"] = "1"
            },
            cancellationToken,
            _origin).ConfigureAwait(false);
        return await QuickContentParser.ParseAsync(
            html,
            _origin,
            mediaId,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<PageResult<CatalogItem>> LoadListingAsync(
        string rootPath,
        int page,
        IReadOnlyDictionary<string, string?>? query,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        var path = page == 1
            ? rootPath
            : $"{rootPath}page/{page}/";
        var html = await _transport.GetSharedStringAsync(
            new Uri(_origin, path),
            query,
            cancellationToken).ConfigureAwait(false);
        return await CatalogParser.ParsePageAsync(
            html,
            _origin,
            page,
            cancellationToken).ConfigureAwait(false);
    }

    private static string GetFilter(CatalogSection section) =>
        section switch
        {
            CatalogSection.Latest => "last",
            CatalogSection.Popular => "popular",
            CatalogSection.Upcoming => "soon",
            CatalogSection.Watching => "watching",
            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };

    private static string? GetGenre(MediaCategory category) =>
        category switch
        {
            MediaCategory.Unknown => null,
            MediaCategory.Film => "1",
            MediaCategory.Series => "2",
            MediaCategory.Cartoon => "3",
            MediaCategory.Anime => "82",
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };
}
