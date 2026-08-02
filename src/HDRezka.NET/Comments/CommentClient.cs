using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using HdRezka.Http;
using HdRezka.Scraping;

namespace HdRezka;

/// <summary>
/// Loads, creates, replies to, and deletes comments through website endpoints
/// </summary>
public sealed class CommentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

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

    /// <summary>
    /// Creates a root comment for the current media page
    /// </summary>
    /// <param name="text">
    /// Comment text accepted by the website rules with optional supported BBCode
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel comment submission and response parsing
    /// </param>
    /// <returns>
    /// Assigned comment identifier, moderation state, and website response text
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="text"/> is empty or contains only whitespace
    /// </exception>
    /// <exception cref="CommentOperationException">
    /// Authentication is missing or the website rejected the comment text
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The comment response did not contain a valid identifier or message
    /// </exception>
    /// <exception cref="JsonException">
    /// The comment endpoint returned malformed JSON
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task<CommentSubmission> AddAsync(
        string text,
        CancellationToken cancellationToken = default) =>
        SubmitAsync(text, null, cancellationToken);

    /// <summary>
    /// Creates a reply below an existing comment
    /// </summary>
    /// <param name="parentCommentId">
    /// Positive identifier of the comment being answered
    /// </param>
    /// <param name="text">
    /// Reply text accepted by the website rules with optional supported BBCode
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel reply submission and response parsing
    /// </param>
    /// <returns>
    /// Assigned reply identifier, parent identifier, moderation state, and website response text
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="parentCommentId"/> is less than one
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="text"/> is empty or contains only whitespace
    /// </exception>
    /// <exception cref="CommentOperationException">
    /// Authentication is missing, the parent is unavailable, or the website rejected the reply
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The reply response did not contain a valid identifier or message
    /// </exception>
    /// <exception cref="JsonException">
    /// The comment endpoint returned malformed JSON
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task<CommentSubmission> ReplyAsync(
        long parentCommentId,
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(parentCommentId, 1);
        return SubmitAsync(text, parentCommentId, cancellationToken);
    }

    /// <summary>
    /// Deletes a comment owned by the current authenticated account
    /// </summary>
    /// <param name="commentId">
    /// Positive identifier of the comment to delete
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel security token loading and comment deletion
    /// </param>
    /// <returns>
    /// A task that completes after the website confirms deletion
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="commentId"/> is less than one
    /// </exception>
    /// <exception cref="LoginRequiredException">
    /// The website returned its login page while loading the account security token
    /// </exception>
    /// <exception cref="CaptchaException">
    /// The website requested captcha verification
    /// </exception>
    /// <exception cref="CommentOperationException">
    /// The comment does not belong to the account, is unavailable, or deletion was rejected
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The security token or deletion response could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// An HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task DeleteAsync(
        long commentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(commentId, 1);
        var settingsHtml = await _transport.GetStringAsync(
            new Uri(_origin, "/settings/"),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var form = await AccountParser.ParseUpdateFormAsync(
            settingsHtml,
            _origin,
            cancellationToken).ConfigureAwait(false);
        var responseText = await _transport.GetStringAsync(
            new Uri(_origin, "/engine/ajax/deletecomments.php"),
            new Dictionary<string, string?>
            {
                ["id"] = commentId.ToString(CultureInfo.InvariantCulture),
                ["dle_allow_hash"] = form.SecurityToken,
                ["type"] = "0",
                ["area"] = "ajax"
            },
            cancellationToken).ConfigureAwait(false);
        CommentDeleteResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<CommentDeleteResponse>(
                responseText.Trim('\uFEFF', ' ', '\r', '\n', '\t'),
                JsonOptions);
        }
        catch (JsonException exception)
        {
            if (responseText.Trim('\uFEFF', ' ', '\r', '\n', '\t')
                .Equals("error", StringComparison.OrdinalIgnoreCase))
            {
                throw new CommentOperationException(
                    "The website rejected comment deletion.");
            }

            throw new ParseException("The comment deletion response is not valid JSON.", exception);
        }

        if (response is null)
        {
            throw new ParseException("The comment deletion response is empty.");
        }

        if (!response.Success)
        {
            var message = await ParseMessageAsync(response.Message, cancellationToken)
                .ConfigureAwait(false);
            throw new CommentOperationException(
                string.IsNullOrWhiteSpace(message)
                    ? "The website rejected comment deletion."
                    : message);
        }
    }

    /// <summary>
    /// Toggles the authenticated account's like on a comment
    /// </summary>
    /// <param name="commentId">
    /// Positive identifier of the comment to like or unlike
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the request
    /// </param>
    /// <returns>
    /// Resulting account reaction and updated total like count
    /// </returns>
    /// <remarks>
    /// The website exposes a toggle endpoint, so the resulting state is returned by the response
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="commentId"/> is less than one
    /// </exception>
    /// <exception cref="CommentOperationException">
    /// Authentication is missing, the account owns the comment, or the website rejected the operation
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The response does not contain a valid status or like count
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The like endpoint returned malformed JSON
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<CommentLikeResult> ToggleLikeAsync(
        long commentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(commentId, 1);
        var response = await _transport.GetJsonAsync<CommentLikeResponse>(
            new Uri(_origin, "/engine/ajax/comments_like.php"),
            new Dictionary<string, string?>
            {
                ["id"] = commentId.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken).ConfigureAwait(false);
        if (!response.Success)
        {
            var message = await ParseMessageAsync(response.Message, cancellationToken)
                .ConfigureAwait(false);
            throw new CommentOperationException(
                string.IsNullOrWhiteSpace(message)
                    ? "The website rejected the comment like."
                    : message);
        }

        if (response.Count < 0)
        {
            throw new ParseException("The comment like response contains a negative count.");
        }

        return new CommentLikeResult(
            !string.Equals(response.Type, "minus", StringComparison.OrdinalIgnoreCase),
            response.Count);
    }

    /// <summary>
    /// Loads accounts that liked one comment
    /// </summary>
    /// <param name="commentId">
    /// Positive comment identifier
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the request and popup fragment parsing
    /// </param>
    /// <returns>
    /// Accounts in website order with profile and avatar URLs when available
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="commentId"/> is less than one
    /// </exception>
    /// <exception cref="CommentOperationException">
    /// The website rejected the request
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The response or a like user could not be read
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The likes endpoint returned malformed JSON
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<IReadOnlyList<CommentLikeUser>> GetLikeUsersAsync(
        long commentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(commentId, 1);
        var response = await _transport.PostFormJsonAsync<CommentLikeUsersResponse>(
            new Uri(_origin, "/ajax/comments_likes/"),
            new Dictionary<string, string>
            {
                ["comment_id"] = commentId.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken,
            _mediaUrl).ConfigureAwait(false);
        if (!response.Success)
        {
            throw new CommentOperationException(
                string.IsNullOrWhiteSpace(response.Message)
                    ? "The website rejected loading comment likes."
                    : await CommentParser.ParseMessageAsync([response.Message], cancellationToken)
                        .ConfigureAwait(false));
        }

        return await CommentParser.ParseLikeUsersAsync(
            response.Message ?? "",
            _origin,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a complaint about a comment from the authenticated account
    /// </summary>
    /// <param name="commentId">
    /// Positive comment identifier
    /// </param>
    /// <param name="issueId">
    /// Non-negative issue identifier exposed by the website report dialog
    /// </param>
    /// <param name="description">
    /// Optional explanation, or <see langword="null"/> when the selected issue does not require text
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel complaint submission
    /// </param>
    /// <returns>
    /// A task that completes after the website confirms the complaint
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="commentId"/> is less than one or <paramref name="issueId"/> is negative
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A non-empty <paramref name="description"/> is shorter than three characters
    /// </exception>
    /// <exception cref="CommentOperationException">
    /// Authentication is missing or the website rejected the complaint
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The complaint response status could not be read
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The complaint endpoint returned malformed JSON
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task ReportAsync(
        long commentId,
        int issueId,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(commentId, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(issueId);
        var text = description?.Trim() ?? "";
        if (text.Length is > 0 and < 3)
        {
            throw new ArgumentException("The complaint description must contain at least three characters.", nameof(description));
        }

        var response = await _transport.PostFormJsonAsync<CommentComplaintResponse>(
            new Uri(_origin, "/engine/ajax/complaint.php"),
            new Dictionary<string, string>
            {
                ["id"] = commentId.ToString(CultureInfo.InvariantCulture),
                ["issue_id"] = issueId.ToString(CultureInfo.InvariantCulture),
                ["text"] = text,
                ["action"] = "comments"
            },
            cancellationToken,
            _mediaUrl).ConfigureAwait(false);
        if (!response.Success)
        {
            throw new CommentOperationException(
                string.IsNullOrWhiteSpace(response.Message)
                    ? "The website rejected the comment complaint."
                    : response.Message.Trim());
        }
    }

    private async Task<CommentSubmission> SubmitAsync(
        string text,
        long? parentCommentId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var parentValue = parentCommentId?.ToString(CultureInfo.InvariantCulture) ?? "0";
        var response = await _transport.PostFormJsonAsync<CommentSubmissionResponse>(
            new Uri(_origin, "/ajax/add_comment/"),
            new Dictionary<string, string>
            {
                ["name"] = "",
                ["mail"] = "",
                ["comments"] = text.Trim(),
                ["post_id"] = _mediaId.ToString(CultureInfo.InvariantCulture),
                ["type"] = "0",
                ["parent"] = parentValue,
                ["replyto_id"] = parentValue,
                ["sec_code"] = "",
                ["question_answer"] = "",
                ["g_recaptcha_response"] = "",
                ["is_admin"] = "",
                ["has_adb"] = "2"
            },
            cancellationToken,
            _mediaUrl).ConfigureAwait(false);
        var message = await ParseMessageAsync(response.Message, cancellationToken)
            .ConfigureAwait(false);
        if (!response.Success)
        {
            throw new CommentOperationException(
                string.IsNullOrWhiteSpace(message)
                    ? "The website rejected the comment."
                    : message);
        }

        if (response.CommentId < 1)
        {
            throw new ParseException("The comment response has no valid identifier.");
        }

        return new CommentSubmission(
            response.CommentId,
            parentCommentId,
            response.OnModeration,
            message);
    }

    private static Task<string> ParseMessageAsync(
        JsonElement message,
        CancellationToken cancellationToken)
    {
        var fragments = message.ValueKind switch
        {
            JsonValueKind.String => [message.GetString() ?? ""],
            JsonValueKind.Array => message
                .EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String
                    ? item.GetString() ?? ""
                    : item.ToString()),
            JsonValueKind.Null or JsonValueKind.Undefined => [],
            _ => [message.ToString()]
        };
        return CommentParser.ParseMessageAsync(fragments, cancellationToken);
    }

    private sealed record CommentResponse(
        [property: JsonPropertyName("comments")] string? Comments,
        [property: JsonPropertyName("navigation")] string? Navigation,
        [property: JsonPropertyName("last_update_id")] long? LastUpdateId);

    private sealed record CommentSubmissionResponse(
        bool Success,
        [property: JsonPropertyName("on_moderation")] bool OnModeration,
        [property: JsonPropertyName("comment_id")] long CommentId,
        JsonElement Message);

    private sealed record CommentDeleteResponse(
        bool Success,
        JsonElement Message);

    private sealed record CommentLikeResponse(
        bool Success,
        int Count,
        string? Type,
        JsonElement Message);

    private sealed record CommentLikeUsersResponse(
        bool Success,
        string? Message);

    private sealed record CommentComplaintResponse(
        bool Success,
        string? Message);
}
