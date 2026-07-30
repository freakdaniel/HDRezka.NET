using System.Globalization;
using System.Text.RegularExpressions;
using HdRezka.Http;
using HdRezka.Scraping;

namespace HdRezka;

/// <summary>
/// Loads curated collections and the media contained in them
/// </summary>
public sealed partial class CollectionClient
{
    private readonly HttpTransport _transport;
    private readonly Uri _origin;

    internal CollectionClient(HttpTransport transport, Uri origin)
    {
        _transport = transport;
        _origin = origin;
    }

    /// <summary>
    /// Loads one page from the website collection directory
    /// </summary>
    /// <param name="page">
    /// One-based page number
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Collection summaries and pagination information
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
    /// A collection summary or response page could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<PageResult<CollectionSummary>> GetPageAsync(
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        var path = page == 1 ? "/collections/" : $"/collections/page/{page}/";
        var html = await _transport.GetStringAsync(
            new Uri(_origin, path),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return await CollectionParser.ParseDirectoryAsync(
            html,
            _origin,
            page,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads one page from a collection URL
    /// </summary>
    /// <param name="url">
    /// Absolute collection URL or a path relative to the configured website origin
    /// </param>
    /// <param name="page">
    /// One-based page number inside the collection
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Collection metadata, media cards, and pagination information
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> is empty, is not a collection URL, or does not contain a collection identifier
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
    /// Collection metadata, a media card, or response data could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<CollectionPage> GetAsync(
        string url,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        var collectionUri = ResolveCollectionUri(url);
        var collectionId = ParseCollectionId(collectionUri);
        var pageUri = page == 1
            ? collectionUri
            : new Uri(collectionUri, $"page/{page}/");
        var html = await _transport.GetStringAsync(
            pageUri,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return await CollectionParser.ParseCollectionAsync(
            html,
            pageUri,
            collectionId,
            page,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads one page from a collection returned by <see cref="GetPageAsync"/>
    /// </summary>
    /// <param name="collection">
    /// Collection summary containing the page URL
    /// </param>
    /// <param name="page">
    /// One-based page number inside the collection
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Collection metadata, media cards, and pagination information
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="collection"/> is <see langword="null"/>
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
    /// Collection metadata, a media card, or response data could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task<CollectionPage> GetAsync(
        CollectionSummary collection,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collection);
        return GetAsync(collection.Url.AbsoluteUri, page, cancellationToken);
    }

    private Uri ResolveCollectionUri(string value)
    {
        var uri = Uri.TryCreate(value, UriKind.Absolute, out var absolute)
            ? new Uri(_origin, absolute.PathAndQuery)
            : new Uri(_origin, value);
        var match = CollectionPathRegex().Match(uri.AbsolutePath);
        if (!match.Success)
        {
            throw new ArgumentException(
                "A collection URL in the form \"/collections/{id}-{name}/\" is required.",
                nameof(value));
        }

        return new Uri(_origin, match.Value.TrimEnd('/') + "/");
    }

    private static int ParseCollectionId(Uri uri)
    {
        var match = CollectionPathRegex().Match(uri.AbsolutePath);
        return match.Success &&
            int.TryParse(
                match.Groups["id"].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var id)
                ? id
                : throw new ArgumentException(
                    "The collection URL does not contain a numeric identifier.",
                    nameof(uri));
    }

    [GeneratedRegex(@"^/collections/(?<id>\d+)-[^/]+/?", RegexOptions.IgnoreCase)]
    private static partial Regex CollectionPathRegex();
}
