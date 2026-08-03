using System.Globalization;
using System.Text.RegularExpressions;
using HdRezka.Http;
using HdRezka.Scraping;

namespace HdRezka;

/// <summary>
/// Loads the franchise directory and ordered franchise parts
/// </summary>
public sealed partial class FranchiseClient
{
    private readonly HttpTransport _transport;
    private readonly Uri _origin;

    internal FranchiseClient(HttpTransport transport, Uri origin)
    {
        _transport = transport;
        _origin = origin;
    }

    /// <summary>
    /// Loads one page from the franchise directory
    /// </summary>
    /// <param name="page">
    /// One-based page number
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Franchise summaries and pagination information
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
    /// A franchise card or response page could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<PageResult<FranchiseSummary>> GetPageAsync(
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        var path = page == 1 ? "/franchises/" : $"/franchises/page/{page}/";
        var html = await _transport.GetSharedStringAsync(
            new Uri(_origin, path),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return await FranchiseParser.ParseDirectoryAsync(
            html,
            _origin,
            page,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads a franchise returned by <see cref="GetPageAsync"/>
    /// </summary>
    /// <param name="franchise">
    /// Franchise summary containing the page and cover URLs
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Complete ordered franchise
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="franchise"/> is <see langword="null"/>
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
    /// Franchise metadata or a part could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<Franchise> GetAsync(
        FranchiseSummary franchise,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(franchise);
        var result = await GetAsync(franchise.Url.AbsoluteUri, cancellationToken)
            .ConfigureAwait(false);
        return result with { ImageUrl = franchise.ImageUrl };
    }

    /// <summary>
    /// Loads an ordered franchise by absolute URL or relative website path
    /// </summary>
    /// <param name="url">
    /// Franchise URL in the form <c>/franchises/{id}-{name}/</c>
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Complete ordered franchise
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> is empty or does not identify a franchise
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
    /// Franchise metadata or a part could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<Franchise> GetAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        var resolved = Uri.TryCreate(url, UriKind.Absolute, out var absolute)
            ? new Uri(_origin, absolute.PathAndQuery)
            : new Uri(_origin, url);
        var match = FranchisePathRegex().Match(resolved.AbsolutePath);
        if (!match.Success)
        {
            throw new ArgumentException(
                "A franchise URL in the form \"/franchises/{id}-{name}/\" is required.",
                nameof(url));
        }

        resolved = new Uri(_origin, match.Value.TrimEnd('/') + "/");
        var id = int.Parse(
            match.Groups["id"].Value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture);
        var html = await _transport.GetSharedStringAsync(
            resolved,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return await FranchiseParser.ParseAsync(
            html,
            resolved,
            id,
            cancellationToken).ConfigureAwait(false);
    }

    [GeneratedRegex(@"^/franchises/(?<id>\d+)-[^/]+/?", RegexOptions.IgnoreCase)]
    private static partial Regex FranchisePathRegex();
}
