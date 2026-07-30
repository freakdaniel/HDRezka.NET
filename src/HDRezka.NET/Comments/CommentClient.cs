using System.Globalization;
using System.Text.Json.Serialization;
using HdRezka.Http;
using HdRezka.Scraping;

namespace HdRezka;

/// <summary>
/// Loads paginated comments through the website AJAX endpoint
/// </summary>
public sealed class CommentClient
{
    private readonly HttpTransport _transport;
    private readonly Uri _origin;
    private readonly Uri _mediaUrl;
    private readonly int _mediaId;

    internal CommentClient(HttpTransport transport, Uri mediaUrl, int mediaId)
    {
        _transport = transport;
        _origin = new Uri(mediaUrl.GetLeftPart(UriPartial.Authority));
        _mediaUrl = mediaUrl;
        _mediaId = mediaId;
    }

    /// <summary>
    /// Loads one comment page without reloading the complete media page
    /// </summary>
    /// <param name="page">
    /// One-based comment page number
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the AJAX request and HTML fragment parsing
    /// </param>
    /// <returns>
    /// Parsed comments, pagination information, and the latest update identifier
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="page"/> is less than one
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The JSON response or a required comment field could not be read
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The comments endpoint returned malformed JSON
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<CommentPage> GetPageAsync(
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        var response = await _transport.GetJsonAsync<CommentResponse>(
            new Uri(_origin, "/ajax/get_comments/"),
            new Dictionary<string, string?>
            {
                ["news_id"] = _mediaId.ToString(CultureInfo.InvariantCulture),
                ["cstart"] = page.ToString(CultureInfo.InvariantCulture),
                ["type"] = "0",
                ["comment_id"] = "0",
                ["skin"] = "hdrezka",
                ["t"] = DateTimeOffset.UtcNow
                    .ToUnixTimeMilliseconds()
                    .ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken).ConfigureAwait(false);
        return await CommentParser.ParseAsync(
            response.Comments,
            response.Navigation,
            _origin,
            _mediaUrl,
            page,
            response.LastUpdateId,
            cancellationToken).ConfigureAwait(false);
    }

    private sealed record CommentResponse(
        [property: JsonPropertyName("comments")] string? Comments,
        [property: JsonPropertyName("navigation")] string? Navigation,
        [property: JsonPropertyName("last_update_id")] long? LastUpdateId);
}
