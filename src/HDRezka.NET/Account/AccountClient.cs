using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using HdRezka.Http;
using HdRezka.Scraping;

namespace HdRezka;

/// <summary>
/// Loads profile metadata, saved viewing positions, and user bookmarks
/// </summary>
public sealed class AccountClient
{
    private readonly HttpTransport _transport;
    private readonly Uri _origin;

    internal AccountClient(HttpTransport transport, Uri origin)
    {
        _transport = transport;
        _origin = origin;
    }

    /// <summary>
    /// Loads metadata for the current authenticated account
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel profile loading and parsing
    /// </param>
    /// <returns>
    /// Account identifier, username, email, avatar, subscription tier, and saved position count
    /// </returns>
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
    /// Required profile metadata or response data could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<AccountProfile> GetProfileAsync(
        CancellationToken cancellationToken = default)
    {
        var html = await _transport.GetStringAsync(
            new Uri(_origin, "/settings/"),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return await AccountParser.ParseProfileAsync(
            html,
            _origin,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads every saved viewing position from the continue-watching page
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel history loading and parsing
    /// </param>
    /// <returns>
    /// Saved viewing positions in website order
    /// </returns>
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
    /// A saved viewing position or response page could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<IReadOnlyList<ContinueWatchingEntry>> GetContinueWatchingAsync(
        CancellationToken cancellationToken = default)
    {
        var html = await _transport.GetStringAsync(
            new Uri(_origin, "/continue/"),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return await AccountParser.ParseContinueWatchingAsync(
            html,
            _origin,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves the latest media, episode, translator, and optional playback position for the authenticated account
    /// </summary>
    /// <param name="progress">
    /// Playback state to synchronize with the website
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the request
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="progress"/> is <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An identifier, season, episode, position, or duration is outside its supported range
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Only one value in a required season, episode, position, or duration pair is supplied
    /// </exception>
    /// <exception cref="AccountOperationException">
    /// The website rejected the playback progress
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The response could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task SavePlaybackProgressAsync(
        PlaybackProgress progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ValidatePositive(progress.MediaId, nameof(progress.MediaId));
        ValidatePositive(progress.TranslatorId, nameof(progress.TranslatorId));
        ValidateOptionalPositive(progress.Season, nameof(progress.Season));
        ValidateOptionalPositive(progress.Episode, nameof(progress.Episode));
        ValidatePlaybackTimes(progress);

        var data = new Dictionary<string, string>
        {
            ["post_id"] = progress.MediaId.ToString(CultureInfo.InvariantCulture),
            ["translator_id"] = progress.TranslatorId.ToString(CultureInfo.InvariantCulture),
            ["season"] = (progress.Season ?? 0).ToString(CultureInfo.InvariantCulture),
            ["episode"] = (progress.Episode ?? 0).ToString(CultureInfo.InvariantCulture)
        };
        if (progress.Position.HasValue)
        {
            data["current_time"] = FormatSeconds(progress.Position.Value);
            data["duration"] = FormatSeconds(progress.Duration!.Value);
        }

        var timestamp = DateTimeOffset.UtcNow
            .ToUnixTimeMilliseconds()
            .ToString(CultureInfo.InvariantCulture);
        return SendMutationAsync(
            new Uri(_origin, $"/ajax/send_save/?t={timestamp}"),
            data,
            "Playback progress could not be saved.",
            cancellationToken);
    }

    /// <summary>
    /// Changes the watched state of one continue-watching entry when needed
    /// </summary>
    /// <param name="entry">
    /// Entry loaded through <see cref="GetContinueWatchingAsync"/>
    /// </param>
    /// <param name="isWatched">
    /// Desired watched state
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the request
    /// </param>
    /// <returns>
    /// Updated immutable entry containing the requested watched state
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="entry"/> is <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The saved position identifier is not positive
    /// </exception>
    /// <exception cref="AccountOperationException">
    /// The website rejected the watched-state change
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The response could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<ContinueWatchingEntry> SetContinueWatchingWatchedAsync(
        ContinueWatchingEntry entry,
        bool isWatched,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ValidatePositive(entry.Id, nameof(entry.Id));
        if (entry.IsWatched == isWatched)
        {
            return entry;
        }

        await SendMutationAsync(
            new Uri(_origin, "/engine/ajax/cdn_saves_view.php"),
            new Dictionary<string, string>
            {
                ["id"] = entry.Id.ToString(CultureInfo.InvariantCulture)
            },
            "The watched state could not be changed.",
            cancellationToken).ConfigureAwait(false);
        return entry with { IsWatched = isWatched };
    }

    /// <summary>
    /// Removes one saved position from the continue-watching list
    /// </summary>
    /// <param name="savedPositionId">
    /// Numeric saved position identifier
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the request
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="savedPositionId"/> is not positive
    /// </exception>
    /// <exception cref="AccountOperationException">
    /// The website rejected the removal
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The response could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task RemoveContinueWatchingAsync(
        long savedPositionId,
        CancellationToken cancellationToken = default)
    {
        ValidatePositive(savedPositionId, nameof(savedPositionId));
        return SendMutationAsync(
            new Uri(_origin, "/engine/ajax/cdn_saves_remove.php"),
            new Dictionary<string, string>
            {
                ["id"] = savedPositionId.ToString(CultureInfo.InvariantCulture)
            },
            "The saved position could not be removed.",
            cancellationToken);
    }

    /// <summary>
    /// Loads every bookmark folder and the media stored in each folder
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel folder loading and parsing
    /// </param>
    /// <returns>
    /// Bookmark folders in website order with their media cards
    /// </returns>
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
    /// A bookmark folder, media card, or response page could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// An HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<IReadOnlyList<BookmarkFolder>> GetBookmarksAsync(
        CancellationToken cancellationToken = default)
    {
        var html = await _transport.GetStringAsync(
            new Uri(_origin, "/favorites/"),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = await AccountParser.ParseBookmarksAsync(
            html,
            _origin,
            cancellationToken).ConfigureAwait(false);
        if (root.Folders.Count == 0)
        {
            return [];
        }

        return await AsyncUtilities.SelectAsync(
            root.Folders,
            _transport.Options.MaxConcurrentRequests,
            (folder, token) => LoadBookmarkFolderAsync(folder, root, token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a bookmark folder for the authenticated account
    /// </summary>
    /// <param name="name">
    /// Human-readable folder name
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the request
    /// </param>
    /// <returns>
    /// Newly created empty bookmark folder
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty or contains only whitespace
    /// </exception>
    /// <exception cref="AccountOperationException">
    /// The website rejected the folder creation
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The response could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<BookmarkFolder> CreateBookmarkFolderAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalizedName = name.Trim();
        var response = await SendMutationWithResponseAsync(
            new Uri(_origin, "/ajax/favorites/"),
            new Dictionary<string, string>
            {
                ["name"] = normalizedName,
                ["action"] = "add_cat"
            },
            "The bookmark folder could not be created.",
            cancellationToken).ConfigureAwait(false);
        var id = ParseRequiredInt64(response.Id, "bookmark folder identifier");
        return new BookmarkFolder(
            id,
            string.IsNullOrWhiteSpace(response.Name) ? normalizedName : response.Name.Trim(),
            0,
            new Uri(_origin, $"/favorites/{id.ToString(CultureInfo.InvariantCulture)}/"),
            []);
    }

    /// <summary>
    /// Deletes a bookmark folder together with every bookmark it contains
    /// </summary>
    /// <param name="folderId">
    /// Numeric bookmark folder identifier
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the request
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="folderId"/> is not positive
    /// </exception>
    /// <exception cref="AccountOperationException">
    /// The website rejected the folder removal
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The response could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task DeleteBookmarkFolderAsync(
        long folderId,
        CancellationToken cancellationToken = default)
    {
        ValidatePositive(folderId, nameof(folderId));
        return SendMutationAsync(
            new Uri(_origin, "/ajax/favorites/"),
            new Dictionary<string, string>
            {
                ["cat_id"] = folderId.ToString(CultureInfo.InvariantCulture),
                ["action"] = "remove_cat"
            },
            "The bookmark folder could not be deleted.",
            cancellationToken);
    }

    /// <summary>
    /// Toggles whether one media item belongs to a bookmark folder
    /// </summary>
    /// <param name="mediaId">
    /// Numeric media identifier
    /// </param>
    /// <param name="folderId">
    /// Numeric bookmark folder identifier
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the request
    /// </param>
    /// <remarks>
    /// The website exposes one toggle operation for both adding and removing a bookmark
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="mediaId"/> or <paramref name="folderId"/> is not positive
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
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task ToggleBookmarkAsync(
        int mediaId,
        long folderId,
        CancellationToken cancellationToken = default)
    {
        ValidatePositive(mediaId, nameof(mediaId));
        ValidatePositive(folderId, nameof(folderId));
        return SendMutationAsync(
            new Uri(_origin, "/ajax/favorites/"),
            new Dictionary<string, string>
            {
                ["post_id"] = mediaId.ToString(CultureInfo.InvariantCulture),
                ["cat_id"] = folderId.ToString(CultureInfo.InvariantCulture),
                ["action"] = "add_post"
            },
            "The bookmark could not be changed.",
            cancellationToken);
    }

    private async Task<BookmarkFolder> LoadBookmarkFolderAsync(
        BookmarkFolderReference folder,
        BookmarkPageSnapshot root,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CatalogItem> items;
        if (root.ActiveFolderId == folder.Id)
        {
            items = root.Items;
        }
        else
        {
            var html = await _transport.GetStringAsync(
                folder.Url,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var page = await AccountParser.ParseBookmarksAsync(
                html,
                _origin,
                cancellationToken).ConfigureAwait(false);
            items = page.Items;
        }

        return new BookmarkFolder(
            folder.Id,
            folder.Name,
            folder.ItemCount,
            folder.Url,
            items);
    }

    private async Task SendMutationAsync(
        Uri uri,
        IReadOnlyDictionary<string, string> data,
        string defaultError,
        CancellationToken cancellationToken)
    {
        _ = await SendMutationWithResponseAsync(
            uri,
            data,
            defaultError,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<AccountMutationResponse> SendMutationWithResponseAsync(
        Uri uri,
        IReadOnlyDictionary<string, string> data,
        string defaultError,
        CancellationToken cancellationToken)
    {
        var response = await _transport.PostFormJsonAsync<AccountMutationResponse>(
            uri,
            data,
            cancellationToken).ConfigureAwait(false);
        if (!TryParseBoolean(response.Success, out var success))
        {
            throw new ParseException("The website returned an invalid account operation status.");
        }

        if (!success)
        {
            throw new AccountOperationException(
                string.IsNullOrWhiteSpace(response.Message)
                    ? defaultError
                    : response.Message.Trim());
        }

        return response;
    }

    private static void ValidatePlaybackTimes(PlaybackProgress progress)
    {
        if (progress.Season.HasValue != progress.Episode.HasValue)
        {
            throw new ArgumentException(
                "Season and episode must either both be supplied or both be omitted.",
                nameof(progress));
        }

        if (progress.Position.HasValue != progress.Duration.HasValue)
        {
            throw new ArgumentException(
                "Playback position and duration must either both be supplied or both be omitted.",
                nameof(progress));
        }

        if (progress.Position is { } position && position < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(progress.Position));
        }

        if (progress.Duration is { } duration && duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(progress.Duration));
        }
    }

    private static void ValidateOptionalPositive(int? value, string parameterName)
    {
        if (value is <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidatePositive(long value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static string FormatSeconds(TimeSpan value) =>
        value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);

    private static long ParseRequiredInt64(JsonElement element, string description) =>
        element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt64(out var value) => value,
            JsonValueKind.String when long.TryParse(
                element.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value) => value,
            _ => throw new ParseException($"The website returned an invalid {description}.")
        };

    private static bool TryParseBoolean(JsonElement element, out bool value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.True:
                value = true;
                return true;
            case JsonValueKind.False:
                value = false;
                return true;
            case JsonValueKind.Number when element.TryGetInt32(out var number):
                value = number != 0;
                return true;
            case JsonValueKind.String when bool.TryParse(element.GetString(), out value):
                return true;
            case JsonValueKind.String when int.TryParse(
                element.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var number):
                value = number != 0;
                return true;
            default:
                value = false;
                return false;
        }
    }

    private sealed record AccountMutationResponse(
        [property: JsonPropertyName("success")] JsonElement Success,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("id")] JsonElement Id,
        [property: JsonPropertyName("name")] string? Name);
}
