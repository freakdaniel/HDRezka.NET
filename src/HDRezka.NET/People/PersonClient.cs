using HdRezka.Http;
using HdRezka.Scraping;

namespace HdRezka;

/// <summary>
/// Loads complete person metadata and filmography
/// </summary>
public sealed class PersonClient
{
    private readonly HttpTransport _transport;
    private readonly Uri _origin;

    internal PersonClient(HttpTransport transport, Uri origin)
    {
        _transport = transport;
        _origin = origin;
    }

    /// <summary>
    /// Loads a person linked from media metadata
    /// </summary>
    /// <param name="person">
    /// Lightweight person reference containing the page URL
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Complete person metadata and filmography
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="person"/> is <see langword="null"/>
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
    /// Required person metadata or filmography could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task<Person> GetAsync(
        PersonInfo person,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(person);
        return GetAsync(person.Url, cancellationToken);
    }

    /// <summary>
    /// Loads a person page by absolute URL or a path relative to the configured website
    /// </summary>
    /// <param name="url">
    /// Absolute person URL or relative website path. An absolute URL is mapped to the configured origin
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Complete person metadata and filmography
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> is empty or does not identify a person page
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
    /// Required person metadata or filmography could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task<Person> GetAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        return GetAsync(new Uri(_origin, url), cancellationToken);
    }

    /// <summary>
    /// Loads a person page by URL
    /// </summary>
    /// <param name="url">
    /// Absolute person URL or relative website URI. An absolute URL is mapped to the configured origin
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel page loading and parsing
    /// </param>
    /// <returns>
    /// Complete person metadata and filmography
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="url"/> is <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> does not identify a person page
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
    /// Required person metadata or filmography could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<Person> GetAsync(
        Uri url,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var resolved = url.IsAbsoluteUri
            ? new Uri(_origin, url.PathAndQuery)
            : new Uri(_origin, url);
        if (!resolved.AbsolutePath.StartsWith("/person/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The URL must identify a person page.", nameof(url));
        }

        var html = await _transport.GetStringAsync(
            resolved,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return await PersonParser.ParseAsync(
            html,
            resolved,
            cancellationToken).ConfigureAwait(false);
    }
}
