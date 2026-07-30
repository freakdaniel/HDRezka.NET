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
        var html = await _transport.GetStringAsync(
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
        var html = await _transport.GetStringAsync(
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
