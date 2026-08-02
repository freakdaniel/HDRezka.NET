using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using HdRezka.Http;
using HdRezka.Scraping;

namespace HdRezka;

/// <summary>
/// Loads and changes profile data, saved viewing positions, and user bookmarks
/// </summary>
public sealed partial class AccountClient
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
    /// Changes the password of the current authenticated account
    /// </summary>
    /// <param name="currentPassword">
    /// Current account password used by the website to authorize the change
    /// </param>
    /// <param name="newPassword">
    /// New password containing at least eight characters
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel settings loading and password submission
    /// </param>
    /// <returns>
    /// Confirmation information returned by the website
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="currentPassword"/> is empty, or <paramref name="newPassword"/> is empty or shorter than eight characters
    /// </exception>
    /// <exception cref="LoginRequiredException">
    /// The website returned its login page
    /// </exception>
    /// <exception cref="CaptchaException">
    /// The website requested captcha verification
    /// </exception>
    /// <exception cref="AccountUpdateException">
    /// The current password is invalid or the website rejected the new password
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The account form or update response could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// An HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<AccountUpdateResult> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPassword);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);
        if (newPassword.Length < 8)
        {
            throw new ArgumentException(
                "The new password must contain at least eight characters.",
                nameof(newPassword));
        }

        var form = await LoadUpdateFormAsync(
            "/settings/security/",
            cancellationToken).ConfigureAwait(false);
        var html = await _transport.PostFormAsync(
            form.Action,
            new Dictionary<string, string>
            {
                ["altpass"] = currentPassword,
                ["password1"] = newPassword,
                ["password2"] = newPassword,
                ["submit"] = "Save",
                ["dosection"] = "security",
                ["doaction"] = "save_security",
                ["username_id"] = form.UserId.ToString(CultureInfo.InvariantCulture),
                ["dle_allow_hash"] = form.SecurityToken
            },
            cancellationToken,
            form.Action).ConfigureAwait(false);
        return await AccountParser.ParseUpdateResponseAsync(html, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Uploads an image and sets a square avatar for the current authenticated account
    /// </summary>
    /// <param name="image">
    /// Readable stream containing a supported image at least 60 by 60 pixels in size
    /// </param>
    /// <param name="fileName">
    /// File name with an image extension used for multipart upload
    /// </param>
    /// <param name="crop">
    /// Square crop in original image pixels, or <see langword="null"/> to use the largest centered square
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel settings loading, image upload, and crop submission
    /// </param>
    /// <returns>
    /// Generated avatar URL, source dimensions, and the applied crop
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="image"/> is <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="image"/> is unreadable or empty, or <paramref name="fileName"/> is empty
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="crop"/> is outside the source image or is too small for the website editor
    /// </exception>
    /// <exception cref="LoginRequiredException">
    /// The website returned its login page
    /// </exception>
    /// <exception cref="CaptchaException">
    /// The website requested captcha verification
    /// </exception>
    /// <exception cref="AccountUpdateException">
    /// The website rejected the image format, dimensions, upload, or crop
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The account form or avatar response could not be read
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// An avatar endpoint returned malformed JSON
    /// </exception>
    /// <exception cref="IOException">
    /// The source image stream could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// An HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<AvatarUpdateResult> SetAvatarAsync(
        Stream image,
        string fileName,
        AvatarCrop? crop = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (!image.CanRead)
        {
            throw new ArgumentException("The avatar stream must be readable.", nameof(image));
        }

        using var buffer = new MemoryStream();
        await image.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (buffer.Length == 0)
        {
            throw new ArgumentException("The avatar stream is empty.", nameof(image));
        }

        var form = await LoadUpdateFormAsync("/settings/", cancellationToken)
            .ConfigureAwait(false);
        var uploadUri = new Uri(
            _origin,
            $"/engine/ajax/upload_avatar.php?user_id={form.UserId.ToString(CultureInfo.InvariantCulture)}&method=put");
        var upload = await _transport.PostMultipartJsonAsync<AvatarUploadResponse>(
            uploadUri,
            new Dictionary<string, string>(),
            "image",
            buffer.ToArray(),
            fileName,
            GetImageContentType(fileName),
            cancellationToken,
            form.Action).ConfigureAwait(false);
        ThrowForRejectedAvatar(upload.Success, upload.Message, "The website rejected the avatar upload.");
        if (string.IsNullOrWhiteSpace(upload.Url) ||
            upload.ImageOriginalWidth < 1 ||
            upload.ImageOriginalHeight < 1)
        {
            throw new ParseException("The avatar upload response has no image data.");
        }

        var resolvedCrop = ResolveCrop(
            crop,
            upload.ImageOriginalWidth,
            upload.ImageOriginalHeight);
        var cropData = CreateCropData(
            upload.Url,
            upload.ImageOriginalWidth,
            upload.ImageOriginalHeight,
            resolvedCrop);
        var cropResponse = await _transport.PostFormJsonAsync<AvatarCropResponse>(
            new Uri(
                _origin,
                $"/engine/ajax/upload_avatar.php?user_id={form.UserId.ToString(CultureInfo.InvariantCulture)}&method=post"),
            cropData,
            cancellationToken,
            form.Action).ConfigureAwait(false);
        ThrowForRejectedAvatar(
            cropResponse.Success,
            cropResponse.Message,
            "The website rejected the avatar crop.");
        if (string.IsNullOrWhiteSpace(cropResponse.Small))
        {
            throw new ParseException("The avatar crop response has no generated image URL.");
        }

        return new AvatarUpdateResult(
            new Uri(_origin, cropResponse.Small),
            upload.ImageOriginalWidth,
            upload.ImageOriginalHeight,
            resolvedCrop);
    }

    /// <summary>
    /// Removes the current avatar from the authenticated account
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel settings loading and avatar removal
    /// </param>
    /// <returns>
    /// Confirmation information returned by the website
    /// </returns>
    /// <exception cref="LoginRequiredException">
    /// The website returned its login page
    /// </exception>
    /// <exception cref="CaptchaException">
    /// The website requested captcha verification
    /// </exception>
    /// <exception cref="AccountUpdateException">
    /// The website rejected the avatar removal
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The account form or update response could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// An HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<AccountUpdateResult> RemoveAvatarAsync(
        CancellationToken cancellationToken = default)
    {
        var form = await LoadUpdateFormAsync("/settings/", cancellationToken)
            .ConfigureAwait(false);
        var html = await _transport.PostFormAsync(
            form.Action,
            new Dictionary<string, string>
            {
                ["email"] = form.Email,
                ["gender"] = form.Gender,
                ["del_foto"] = "yes",
                ["submit"] = "Save",
                ["dosection"] = "general",
                ["doaction"] = "save_general",
                ["username_id"] = form.UserId.ToString(CultureInfo.InvariantCulture),
                ["dle_allow_hash"] = form.SecurityToken
            },
            cancellationToken,
            form.Action).ConfigureAwait(false);
        return await AccountParser.ParseUpdateResponseAsync(html, cancellationToken)
            .ConfigureAwait(false);
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
        => await GetBookmarksAsync(
            new BookmarkQuery(),
            cancellationToken).ConfigureAwait(false);

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
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("moved")] JsonElement Moved,
        [property: JsonPropertyName("added")] JsonElement Added);

    private async Task<AccountFormSnapshot> LoadUpdateFormAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var html = await _transport.GetStringAsync(
            new Uri(_origin, path),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return await AccountParser.ParseUpdateFormAsync(
            html,
            _origin,
            cancellationToken).ConfigureAwait(false);
    }

    private static AvatarCrop ResolveCrop(
        AvatarCrop? crop,
        int imageWidth,
        int imageHeight)
    {
        var result = crop ?? new AvatarCrop(
            Math.Max(0, (imageWidth - imageHeight) / 2),
            Math.Max(0, (imageHeight - imageWidth) / 2),
            Math.Min(imageWidth, imageHeight));
        if (result.X < 0 ||
            result.Y < 0 ||
            result.Size < 1 ||
            result.X + result.Size > imageWidth ||
            result.Y + result.Size > imageHeight)
        {
            throw new ArgumentOutOfRangeException(
                nameof(crop),
                "The avatar crop must fit inside the source image.");
        }

        var scale = Math.Min(1d, 525d / imageWidth);
        if (result.Size * scale < 60d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(crop),
                "The avatar crop must be at least 60 pixels in the website editor.");
        }

        return result;
    }

    private static Dictionary<string, string> CreateCropData(
        string temporaryFile,
        int imageWidth,
        int imageHeight,
        AvatarCrop crop)
    {
        var scale = Math.Min(1d, 525d / imageWidth);
        var scaledWidth = imageWidth * scale;
        var scaledHeight = imageHeight * scale;
        var scaledCropSize = crop.Size * scale;
        return new Dictionary<string, string>
        {
            ["x1"] = Round(crop.X * scale),
            ["y1"] = Round(crop.Y * scale),
            ["width"] = ((int)scaledWidth).ToString(CultureInfo.InvariantCulture),
            ["height"] = ((int)scaledHeight).ToString(CultureInfo.InvariantCulture),
            ["twidth_small"] = Round(60d / scaledCropSize * scaledWidth),
            ["theight_small"] = Round(60d / scaledCropSize * scaledHeight),
            ["tempfile"] = temporaryFile
        };
    }

    private static string Round(double value) =>
        ((int)Math.Round(value, MidpointRounding.AwayFromZero))
            .ToString(CultureInfo.InvariantCulture);

    private static string GetImageContentType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

    private static void ThrowForRejectedAvatar(
        bool success,
        string? message,
        string fallbackMessage)
    {
        if (!success)
        {
            throw new AccountUpdateException(
                string.IsNullOrWhiteSpace(message) ? fallbackMessage : message.Trim());
        }
    }

    private sealed record AvatarUploadResponse(
        bool Success,
        string? Message,
        string? Url,
        int ImageOriginalWidth,
        int ImageOriginalHeight);

    private sealed record AvatarCropResponse(
        bool Success,
        string? Message,
        string? Small);
}
