using System.Globalization;
using HdRezka.Abstractions;
using HdRezka.Http;
using HdRezka.Scraping;

namespace HdRezka;

/// <summary>
/// Searches one HDRezka-compatible website and keeps shared request settings between calls
/// </summary>
public sealed class SearchClient : IDisposable
{
    private readonly HttpTransport _transport;
    private readonly IScraper _scraper;
    private readonly bool _ownsTransport;

    /// <summary>
    /// Creates a search client for a website origin
    /// </summary>
    /// <param name="origin">
    /// Absolute website origin such as <c>https://example.com</c>
    /// </param>
    /// <param name="options">
    /// Request settings, or <see langword="null"/> to use the defaults
    /// </param>
    /// <param name="httpClient">
    /// HTTP client used for requests, or <see langword="null"/> to create an internal client.
    /// The supplied client remains owned by the caller
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="origin"/> is <see langword="null"/>
    /// </exception>
    /// <exception cref="UriFormatException">
    /// <paramref name="origin"/> is not a valid absolute URI
    /// </exception>
    public SearchClient(
        string origin,
        ClientOptions? options = null,
        HttpClient? httpClient = null)
        : this(
            new HttpTransport((options ?? new ClientOptions()).Clone(), httpClient),
            ownsTransport: true,
            new Uri(origin, UriKind.Absolute))
    {
    }

    /// <summary>
    /// Creates a search client for a website origin
    /// </summary>
    /// <param name="origin">
    /// Absolute website origin
    /// </param>
    /// <param name="options">
    /// Request settings, or <see langword="null"/> to use the defaults
    /// </param>
    /// <param name="httpClient">
    /// HTTP client used for requests, or <see langword="null"/> to create an internal client.
    /// The supplied client remains owned by the caller
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="origin"/> is <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="origin"/> is relative
    /// </exception>
    public SearchClient(
        Uri origin,
        ClientOptions? options = null,
        HttpClient? httpClient = null)
        : this(
            new HttpTransport((options ?? new ClientOptions()).Clone(), httpClient),
            ownsTransport: true,
            origin)
    {
    }

    internal SearchClient(HttpTransport transport, bool ownsTransport, Uri origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (!origin.IsAbsoluteUri)
        {
            throw new ArgumentException("An absolute origin is required.", nameof(origin));
        }

        _transport = transport;
        _scraper = new Scraper();
        _ownsTransport = ownsTransport;
        Origin = new Uri(origin.GetLeftPart(UriPartial.Authority));
    }

    /// <summary>
    /// Gets the normalized website origin used for every search request
    /// </summary>
    /// <value>
    /// Scheme and host of the configured website
    /// </value>
    public Uri Origin { get; }

    /// <summary>
    /// Searches the compact suggestion endpoint
    /// </summary>
    /// <param name="query">
    /// Text entered into the website search
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the request and response parsing
    /// </param>
    /// <returns>
    /// Compact results returned for the query
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="query"/> is empty or contains only whitespace
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A configured header cannot be added to the request
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// A search result or response data could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<IReadOnlyList<FastSearchResult>> FastSearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var html = await _transport.PostFormAsync(
            new Uri(Origin, "/engine/ajax/search.php"),
            new Dictionary<string, string> { ["q"] = query },
            cancellationToken).ConfigureAwait(false);
        return await _scraper.ParseFastSearchAsync(html, Origin, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Loads one page from the full website search
    /// </summary>
    /// <param name="query">
    /// Text entered into the website search
    /// </param>
    /// <param name="page">
    /// One-based page number
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the request and response parsing
    /// </param>
    /// <returns>
    /// Full search results found on the requested page
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="query"/> is empty or contains only whitespace
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="page"/> is less than one
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A configured header cannot be added to the request
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
    /// A search result or response data could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<IReadOnlyList<SearchResult>> SearchPageAsync(
        string query,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        return (await LoadSearchPageAsync(query, page, cancellationToken).ConfigureAwait(false))
            .Items;
    }

    /// <summary>
    /// Loads detected full-search pages concurrently up to the configured limit
    /// </summary>
    /// <param name="query">
    /// Text entered into the website search
    /// </param>
    /// <param name="maximumPages">
    /// Maximum number of pages to load, or <see langword="null"/> to use the total detected from page navigation
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel any request or response parsing
    /// </param>
    /// <returns>
    /// Combined full search results in page order
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="query"/> is empty or contains only whitespace
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maximumPages"/> is less than one
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A configured header cannot be added to the request
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
    /// A search result or response data could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<IReadOnlyList<SearchResult>> SearchAllAsync(
        string query,
        int? maximumPages = null,
        CancellationToken cancellationToken = default)
    {
        if (maximumPages is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPages));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var first = await LoadSearchPageAsync(query, 1, cancellationToken).ConfigureAwait(false);
        if (first.Items.Count == 0)
        {
            return [];
        }

        var lastPage = Math.Min(maximumPages ?? first.TotalPages, first.TotalPages);
        var remainingPages = Enumerable.Range(2, Math.Max(0, lastPage - 1));
        var remaining = await AsyncUtilities.SelectAsync(
            remainingPages,
            _transport.Options.MaxConcurrentRequests,
            (page, token) => LoadSearchPageAsync(query, page, token),
            cancellationToken).ConfigureAwait(false);
        var results = new List<SearchResult>(first.Items);
        foreach (var page in remaining)
        {
            results.AddRange(page.Items);
        }

        return results;
    }

    private async Task<PageResult<SearchResult>> LoadSearchPageAsync(
        string query,
        int page,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        var html = await _transport.GetStringAsync(
            new Uri(Origin, "/search/"),
            new Dictionary<string, string?>
            {
                ["do"] = "search",
                ["subaction"] = "search",
                ["q"] = query,
                ["page"] = page.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken).ConfigureAwait(false);
        return await _scraper.ParseSearchPageAsync(
            html,
            Origin,
            page,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Releases the internally created HTTP client while leaving a supplied <see cref="HttpClient"/> untouched
    /// </summary>
    public void Dispose()
    {
        if (_ownsTransport)
        {
            _transport.Dispose();
        }
    }
}
