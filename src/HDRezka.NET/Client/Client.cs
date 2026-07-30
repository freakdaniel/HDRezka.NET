using HdRezka.Http;
using HdRezka.Scraping;

namespace HdRezka;

/// <summary>
/// Provides one entry point for account data, catalogs, collections, authentication, media, and search while sharing cookies and HTTP settings
/// </summary>
public sealed class Client : IDisposable
{
    private readonly HttpTransport _transport;
    private readonly AuthenticationService? _authentication;

    /// <summary>
    /// Creates a client for an optional website origin
    /// </summary>
    /// <param name="origin">
    /// Absolute website origin such as <c>https://example.com</c>, or <see langword="null"/> to allow only absolute media URLs
    /// </param>
    /// <param name="options">
    /// Request and translator settings, or <see langword="null"/> to use the defaults
    /// </param>
    /// <param name="httpClient">
    /// HTTP client used for requests, or <see langword="null"/> to create an internal client.
    /// The supplied client remains owned by the caller
    /// </param>
    /// <exception cref="UriFormatException">
    /// <paramref name="origin"/> is not a valid absolute URI
    /// </exception>
    public Client(
        string? origin = null,
        ClientOptions? options = null,
        HttpClient? httpClient = null)
        : this(
            string.IsNullOrWhiteSpace(origin) ? null : new Uri(origin, UriKind.Absolute),
            options,
            httpClient)
    {
    }

    /// <summary>
    /// Creates a client for an optional website origin
    /// </summary>
    /// <param name="origin">
    /// Absolute website origin, or <see langword="null"/> to allow only absolute media URLs
    /// </param>
    /// <param name="options">
    /// Request and translator settings, or <see langword="null"/> to use the defaults
    /// </param>
    /// <param name="httpClient">
    /// HTTP client used for requests, or <see langword="null"/> to create an internal client.
    /// The supplied client remains owned by the caller
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="origin"/> is relative
    /// </exception>
    public Client(
        Uri? origin,
        ClientOptions? options = null,
        HttpClient? httpClient = null)
    {
        Options = (options ?? new ClientOptions()).Clone();
        _transport = new HttpTransport(Options, httpClient);
        Origin = origin is null ? null : new Uri(origin.GetLeftPart(UriPartial.Authority));
        if (Origin is not null)
        {
            _authentication = new AuthenticationService(
                Origin,
                _transport,
                new AuthenticationPageInspector());
        }
    }

    /// <summary>
    /// Gets the normalized website origin used for relative URLs, authentication, and search
    /// </summary>
    /// <value>
    /// Scheme and host of the configured website, or <see langword="null"/> when no origin was supplied
    /// </value>
    public Uri? Origin { get; }

    /// <summary>
    /// Gets the private copy of request and translator settings used by this client
    /// </summary>
    /// <value>
    /// Settings that can be changed for subsequent requests without changing the object passed to the constructor
    /// </value>
    public ClientOptions Options { get; }

    /// <summary>
    /// Gets account metadata, continue-watching history, and bookmark operations
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The client was created without an origin
    /// </exception>
    public AccountClient Account => new(_transport, RequireOrigin("account data"));

    /// <summary>
    /// Gets home-page catalog section operations
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The client was created without an origin
    /// </exception>
    public CatalogClient Catalog => new(_transport, RequireOrigin("catalogs"));

    /// <summary>
    /// Gets curated collection directory and content operations
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The client was created without an origin
    /// </exception>
    public CollectionClient Collections => new(_transport, RequireOrigin("collections"));

    /// <summary>
    /// Signs in with website credentials and keeps the returned cookies for subsequent requests
    /// </summary>
    /// <param name="email">
    /// Account email or login name
    /// </param>
    /// <param name="password">
    /// Account password
    /// </param>
    /// <param name="rememberMe">
    /// <see langword="true"/> to request persistent authentication cookies, otherwise <see langword="false"/>
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the login request and the following authentication check
    /// </param>
    /// <returns>
    /// Verified authentication state together with the names of cookies stored by the client
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="email"/> or <paramref name="password"/> is empty or contains only whitespace
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The client was created without an origin
    /// </exception>
    /// <exception cref="LoginFailedException">
    /// The website rejected the credentials or the authenticated state could not be verified
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The login response or returned cookies could not be read
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The login endpoint returned malformed JSON
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task<AuthenticationState> LoginAsync(
        string email,
        string password,
        bool rememberMe = true,
        CancellationToken cancellationToken = default)
    {
        if (_authentication is null)
        {
            throw new InvalidOperationException("An origin is required for login.");
        }

        return _authentication.LoginAsync(
            email,
            password,
            rememberMe,
            cancellationToken);
    }

    /// <summary>
    /// Checks whether the current cookies provide access to the authenticated favorites page
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel the authentication check
    /// </param>
    /// <returns>
    /// Current authentication state, verification address, and names of cookies stored by the client
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The client was created without an origin
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The response or returned cookies could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task<AuthenticationState> GetAuthenticationStateAsync(
        CancellationToken cancellationToken = default)
    {
        if (_authentication is null)
        {
            throw new InvalidOperationException(
                "An origin is required to inspect authentication state.");
        }

        return _authentication.GetStateAsync(cancellationToken);
    }

    /// <summary>
    /// Signs out, removes local authentication cookies, and verifies the resulting state
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel the logout request and the following authentication check
    /// </param>
    /// <returns>
    /// Verified authentication state after logout together with the remaining cookie names
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The client was created without an origin
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The response or returned cookies could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task<AuthenticationState> LogoutAsync(
        CancellationToken cancellationToken = default)
    {
        if (_authentication is null)
        {
            throw new InvalidOperationException("An origin is required for logout.");
        }

        return _authentication.LogoutAsync(cancellationToken);
    }

    /// <summary>
    /// Loads and parses a movie or series page
    /// </summary>
    /// <param name="url">
    /// Absolute media URL, or a path resolved against <see cref="Origin"/>.
    /// When an origin is configured, the supplied host is replaced with that origin
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Loaded media metadata and operations for streams, seasons, and episodes
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> is empty, contains only whitespace, or is relative while <see cref="Origin"/> is unavailable
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
    public Task<Media> GetAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        return GetAsync(ResolveUrl(url), cancellationToken);
    }

    /// <summary>
    /// Loads and parses a movie or series page
    /// </summary>
    /// <param name="url">
    /// Absolute media URL, or a relative URI resolved against <see cref="Origin"/>.
    /// When an origin is configured, the supplied host is replaced with that origin
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
    /// <paramref name="url"/> is relative while <see cref="Origin"/> is unavailable
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
    public Task<Media> GetAsync(
        Uri url,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        return Media.LoadAsync(
            _transport,
            ownsTransport: false,
            ResolveUrl(url),
            cancellationToken);
    }

    /// <summary>
    /// Creates a search client that shares this client's cookies, headers, proxy, and HTTP connection
    /// </summary>
    /// <returns>
    /// Search client bound to <see cref="Origin"/>
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The client was created without an origin
    /// </exception>
    public SearchClient CreateSearch()
    {
        if (Origin is null)
        {
            throw new InvalidOperationException("An origin is required for search.");
        }

        return new SearchClient(_transport, ownsTransport: false, Origin);
    }

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
    /// The client was created without an origin or a configured header cannot be added
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
    public Task<IReadOnlyList<FastSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default) =>
        CreateSearch().FastSearchAsync(query, cancellationToken);

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
    /// The client was created without an origin or a configured header cannot be added
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
    public Task<IReadOnlyList<SearchResult>> SearchPageAsync(
        string query,
        int page = 1,
        CancellationToken cancellationToken = default) =>
        CreateSearch().SearchPageAsync(query, page, cancellationToken);

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
    /// The client was created without an origin or a configured header cannot be added
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
    public Task<IReadOnlyList<SearchResult>> SearchAllAsync(
        string query,
        int? maximumPages = null,
        CancellationToken cancellationToken = default) =>
        CreateSearch().SearchAllAsync(query, maximumPages, cancellationToken);

    private Uri ResolveUrl(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            return ResolveUrl(absolute);
        }

        if (Origin is null)
        {
            throw new ArgumentException(
                "A relative URL can only be used when the client has an origin.",
                nameof(value));
        }

        return new Uri(Origin, value);
    }

    private Uri RequireOrigin(string operation) =>
        Origin ??
        throw new InvalidOperationException(
            $"An origin is required to access {operation}.");

    private Uri ResolveUrl(Uri value)
    {
        if (!value.IsAbsoluteUri)
        {
            if (Origin is null)
            {
                throw new ArgumentException(
                    "A relative URL can only be used when the client has an origin.",
                    nameof(value));
            }

            return new Uri(Origin, value);
        }

        if (Origin is null)
        {
            return value;
        }

        return new Uri(Origin, value.PathAndQuery + value.Fragment);
    }

    /// <summary>
    /// Releases the internally created HTTP client while leaving a supplied <see cref="HttpClient"/> untouched
    /// </summary>
    public void Dispose() => _transport.Dispose();
}
