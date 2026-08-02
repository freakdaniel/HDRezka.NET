using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using HdRezka.Http;
using HdRezka.Scraping;

namespace HdRezka;

public sealed partial class AccountClient
{
    /// <summary>
    /// Loads editable email and gender settings for the authenticated account
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel settings loading and parsing
    /// </param>
    /// <returns>
    /// Current editable account settings
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
    /// The settings form could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<AccountSettings> GetSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        var form = await LoadUpdateFormAsync("/settings/", cancellationToken)
            .ConfigureAwait(false);
        return new AccountSettings(form.Email, ParseGender(form.Gender));
    }

    /// <summary>
    /// Changes the authenticated account email and gender
    /// </summary>
    /// <param name="settings">
    /// Complete general settings to save. Both values replace their current website values
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel settings loading and submission
    /// </param>
    /// <returns>
    /// Confirmation information returned by the website
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="settings"/> is <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The email address is empty or invalid, or the gender value is unsupported
    /// </exception>
    /// <exception cref="AccountUpdateException">
    /// The website rejected the account settings
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
    /// The settings form or response could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// An HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<AccountUpdateResult> UpdateSettingsAsync(
        AccountSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.Email);
        try
        {
            _ = new MailAddress(settings.Email);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("The email address is invalid.", nameof(settings), exception);
        }

        if (!Enum.IsDefined(settings.Gender))
        {
            throw new ArgumentException("The gender value is unsupported.", nameof(settings));
        }

        var form = await LoadUpdateFormAsync("/settings/", cancellationToken)
            .ConfigureAwait(false);
        var html = await _transport.PostFormAsync(
            form.Action,
            new Dictionary<string, string>
            {
                ["email"] = settings.Email.Trim(),
                ["gender"] = ((int)settings.Gender).ToString(CultureInfo.InvariantCulture),
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
    /// Loads website and player behavior saved for the authenticated account
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel settings loading and parsing
    /// </param>
    /// <returns>
    /// Current playback and navigation preferences
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
    /// The preference form could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<PlaybackPreferences> GetPlaybackPreferencesAsync(
        CancellationToken cancellationToken = default)
    {
        var html = await _transport.GetStringAsync(
            new Uri(_origin, "/settings/personality/"),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return await AccountParser.ParsePreferencesAsync(html, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Replaces website and player behavior for the authenticated account
    /// </summary>
    /// <param name="preferences">
    /// Complete preference values to save
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel settings loading and submission
    /// </param>
    /// <returns>
    /// Confirmation information returned by the website
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="preferences"/> is <see langword="null"/>
    /// </exception>
    /// <exception cref="AccountUpdateException">
    /// The website rejected the preferences
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
    /// The preference form or response could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// An HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<AccountUpdateResult> UpdatePlaybackPreferencesAsync(
        PlaybackPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var form = await LoadUpdateFormAsync("/settings/personality/", cancellationToken)
            .ConfigureAwait(false);
        var data = new Dictionary<string, string>
        {
            ["submit"] = "Save",
            ["dosection"] = "personality",
            ["doaction"] = "save_personality",
            ["username_id"] = form.UserId.ToString(CultureInfo.InvariantCulture),
            ["dle_allow_hash"] = form.SecurityToken
        };
        AddChecked(data, "ctrl_links", preferences.UpdateAddressOnSelection);
        AddChecked(data, "cdn_autoswitch", preferences.AutoSwitchEpisodes);
        AddChecked(data, "cdn_first_episode", preferences.SelectFirstEpisode);
        var html = await _transport.PostFormAsync(
            form.Action,
            data,
            cancellationToken,
            form.Action).ConfigureAwait(false);
        return await AccountParser.ParseUpdateResponseAsync(html, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Loads Premium payment history without starting a payment
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel history loading and parsing
    /// </param>
    /// <returns>
    /// Payment rows in website order
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
    /// A payment row or response page could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<IReadOnlyList<PaymentHistoryEntry>> GetPaymentHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        var html = await _transport.GetStringAsync(
            new Uri(_origin, "/payments/history/"),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return await PaymentParser.ParseHistoryAsync(
            html,
            _origin,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads read-only Premium payment methods and plans without starting checkout
    /// </summary>
    /// <param name="currency">
    /// Website currency selector such as <c>ru</c>, <c>ua</c>, or <c>eu</c>, or <see langword="null"/> to use the website default
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel offer loading and parsing
    /// </param>
    /// <returns>
    /// Available payment methods and plans
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="currency"/> is not a two-letter website currency selector
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
    /// A payment method, plan, or response page could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<PremiumOffers> GetPremiumOffersAsync(
        string? currency = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = currency?.Trim().ToLowerInvariant();
        if (normalized is not null &&
            (normalized.Length != 2 || normalized.Any(character => !char.IsAsciiLetter(character))))
        {
            throw new ArgumentException("The currency selector must contain two ASCII letters.", nameof(currency));
        }

        var html = await _transport.GetStringAsync(
            new Uri(_origin, "/payments/"),
            normalized is null ? null : new Dictionary<string, string?> { ["currency"] = normalized },
            cancellationToken).ConfigureAwait(false);
        return await PaymentParser.ParseOffersAsync(
            html,
            _origin,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads every bookmark folder using website sorting and media filtering
    /// </summary>
    /// <param name="query">
    /// Sorting and category filter applied to every folder
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel folder loading and parsing
    /// </param>
    /// <returns>
    /// Bookmark folders in website order with their filtered media cards
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="query"/> is <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The website does not support the requested category filter
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
    /// A bookmark folder, media card, or response page could not be read
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<IReadOnlyList<BookmarkFolder>> GetBookmarksAsync(
        BookmarkQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var queryValues = CreateBookmarkQuery(query);
        var html = await _transport.GetStringAsync(
            new Uri(_origin, "/favorites/"),
            queryValues,
            cancellationToken).ConfigureAwait(false);
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
            (folder, token) => LoadBookmarkFolderAsync(folder, root, queryValues, token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Renames one bookmark folder
    /// </summary>
    /// <param name="folder">
    /// Folder to rename
    /// </param>
    /// <param name="name">
    /// New non-empty folder name
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the request
    /// </param>
    /// <returns>
    /// Updated immutable bookmark folder
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="folder"/> is <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty or contains only whitespace
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The folder identifier is not positive
    /// </exception>
    /// <exception cref="AccountOperationException">
    /// The website rejected the folder rename
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The response status could not be read
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The bookmark endpoint returned malformed JSON
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<BookmarkFolder> RenameBookmarkFolderAsync(
        BookmarkFolder folder,
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ValidatePositive(folder.Id, nameof(folder));
        var normalized = name.Trim();
        await SendMutationAsync(
            new Uri(_origin, "/ajax/favorites/"),
            new Dictionary<string, string>
            {
                ["name"] = normalized,
                ["cat_id"] = folder.Id.ToString(CultureInfo.InvariantCulture),
                ["action"] = "change_cat_name"
            },
            "The bookmark folder could not be renamed.",
            cancellationToken).ConfigureAwait(false);
        return folder with { Name = normalized };
    }

    /// <summary>
    /// Saves bookmark folder ordering
    /// </summary>
    /// <param name="folderIds">
    /// Positive folder identifiers in the desired order
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the request
    /// </param>
    /// <returns>
    /// A task that completes after the website confirms the ordering
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="folderIds"/> is <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="folderIds"/> is empty
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A folder identifier is not positive
    /// </exception>
    /// <exception cref="AccountOperationException">
    /// The website rejected the folder ordering
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The response status could not be read
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The bookmark endpoint returned malformed JSON
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task SortBookmarkFoldersAsync(
        IEnumerable<long> folderIds,
        CancellationToken cancellationToken = default)
    {
        var ids = ValidateIds(folderIds, nameof(folderIds));
        return SendExtendedMutationAsync(
            CreateRepeatedForm("cats[]", ids, ("action", "sort_cats")),
            "The bookmark folders could not be reordered.",
            cancellationToken);
    }

    /// <summary>
    /// Removes selected media from one bookmark folder
    /// </summary>
    /// <param name="folderId">
    /// Source bookmark folder identifier
    /// </param>
    /// <param name="mediaIds">
    /// Positive media identifiers to remove
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the request
    /// </param>
    /// <returns>
    /// A task that completes after the website confirms removal
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="mediaIds"/> is <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="mediaIds"/> is empty
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The folder identifier or a media identifier is not positive
    /// </exception>
    /// <exception cref="AccountOperationException">
    /// The website rejected the selected bookmark removal
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The response status could not be read
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The bookmark endpoint returned malformed JSON
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public Task RemoveBookmarksAsync(
        long folderId,
        IEnumerable<int> mediaIds,
        CancellationToken cancellationToken = default)
    {
        ValidatePositive(folderId, nameof(folderId));
        var ids = ValidateIds(mediaIds?.Select(value => (long)value), nameof(mediaIds));
        return SendExtendedMutationAsync(
            CreateRepeatedForm(
                "items[]",
                ids,
                ("cat_id", folderId.ToString(CultureInfo.InvariantCulture)),
                ("action", "remove_items")),
            "The selected bookmarks could not be removed.",
            cancellationToken);
    }

    /// <summary>
    /// Moves selected media between bookmark folders
    /// </summary>
    /// <param name="sourceFolderId">
    /// Source bookmark folder identifier
    /// </param>
    /// <param name="destinationFolderId">
    /// Destination bookmark folder identifier
    /// </param>
    /// <param name="mediaIds">
    /// Positive media identifiers to move
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the request
    /// </param>
    /// <returns>
    /// Number of bookmarks reported as moved
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="mediaIds"/> is <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The source and destination are equal or <paramref name="mediaIds"/> is empty
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A folder identifier or media identifier is not positive
    /// </exception>
    /// <exception cref="AccountOperationException">
    /// The website rejected the bookmark move
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The response status or moved count could not be read
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The bookmark endpoint returned malformed JSON
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<BookmarkMoveResult> MoveBookmarksAsync(
        long sourceFolderId,
        long destinationFolderId,
        IEnumerable<int> mediaIds,
        CancellationToken cancellationToken = default)
    {
        ValidateDifferentFolders(sourceFolderId, destinationFolderId);
        var ids = ValidateIds(mediaIds?.Select(value => (long)value), nameof(mediaIds));
        var response = await SendExtendedMutationWithResponseAsync(
            CreateRepeatedForm(
                "items[]",
                ids,
                ("from_cat_id", sourceFolderId.ToString(CultureInfo.InvariantCulture)),
                ("to_cat_id", destinationFolderId.ToString(CultureInfo.InvariantCulture)),
                ("action", "change_items_cat")),
            "The selected bookmarks could not be moved.",
            cancellationToken).ConfigureAwait(false);
        return new BookmarkMoveResult(ParseOptionalCount(response.Moved, ids.Count));
    }

    /// <summary>
    /// Moves every bookmark from one folder to another
    /// </summary>
    /// <param name="sourceFolderId">
    /// Source bookmark folder identifier
    /// </param>
    /// <param name="destinationFolderId">
    /// Destination bookmark folder identifier
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the request
    /// </param>
    /// <returns>
    /// Number of bookmarks reported as added to the destination
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The source and destination folders are equal
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A folder identifier is not positive
    /// </exception>
    /// <exception cref="AccountOperationException">
    /// The website rejected the folder contents move
    /// </exception>
    /// <exception cref="HttpException">
    /// The website returned an unsuccessful HTTP status
    /// </exception>
    /// <exception cref="ParseException">
    /// The response status or added count could not be read
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The bookmark endpoint returned malformed JSON
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The HTTP request could not be completed
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled
    /// </exception>
    public async Task<BookmarkMoveResult> MoveBookmarkFolderAsync(
        long sourceFolderId,
        long destinationFolderId,
        CancellationToken cancellationToken = default)
    {
        ValidateDifferentFolders(sourceFolderId, destinationFolderId);
        var response = await SendExtendedMutationWithResponseAsync(
            new Dictionary<string, string>
            {
                ["from_cat_id"] = sourceFolderId.ToString(CultureInfo.InvariantCulture),
                ["to_cat_id"] = destinationFolderId.ToString(CultureInfo.InvariantCulture),
                ["action"] = "move_to_cat"
            },
            "The bookmark folder contents could not be moved.",
            cancellationToken).ConfigureAwait(false);
        return new BookmarkMoveResult(ParseOptionalCount(response.Added, 0));
    }

    private async Task<BookmarkFolder> LoadBookmarkFolderAsync(
        BookmarkFolderReference folder,
        BookmarkPageSnapshot root,
        IReadOnlyDictionary<string, string?> query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CatalogItem> items;
        if (root.ActiveFolderId == folder.Id)
        {
            items = root.Items;
        }
        else
        {
            var html = await _transport.GetStringAsync(folder.Url, query, cancellationToken)
                .ConfigureAwait(false);
            var page = await AccountParser.ParseBookmarksAsync(
                html,
                _origin,
                cancellationToken).ConfigureAwait(false);
            items = page.Items;
        }

        return new BookmarkFolder(folder.Id, folder.Name, folder.ItemCount, folder.Url, items);
    }

    private async Task SendExtendedMutationAsync(
        IEnumerable<KeyValuePair<string, string>> data,
        string defaultError,
        CancellationToken cancellationToken)
    {
        _ = await SendExtendedMutationWithResponseAsync(data, defaultError, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<AccountMutationResponse> SendExtendedMutationWithResponseAsync(
        IEnumerable<KeyValuePair<string, string>> data,
        string defaultError,
        CancellationToken cancellationToken)
    {
        var response = await _transport.PostFormJsonAsync<AccountMutationResponse>(
            new Uri(_origin, "/ajax/favorites/"),
            data,
            cancellationToken).ConfigureAwait(false);
        if (!TryParseBoolean(response.Success, out var success))
        {
            throw new ParseException("The website returned an invalid bookmark operation status.");
        }

        if (!success)
        {
            throw new AccountOperationException(
                string.IsNullOrWhiteSpace(response.Message) ? defaultError : response.Message.Trim());
        }

        return response;
    }

    private static IReadOnlyDictionary<string, string?> CreateBookmarkQuery(BookmarkQuery query) =>
        new Dictionary<string, string?>
        {
            ["filter"] = query.Sort switch
            {
                BookmarkSort.Added => "added",
                BookmarkSort.Year => "year",
                BookmarkSort.Popular => "popular",
                _ => throw new ArgumentException("The bookmark sort value is unsupported.", nameof(query))
            },
            ["genre"] = query.Category switch
            {
                MediaCategory.Unknown => null,
                MediaCategory.Film => "1",
                MediaCategory.Series => "2",
                MediaCategory.Cartoon => "3",
                MediaCategory.Anime => "82",
                _ => throw new ArgumentException("The website does not support this bookmark category filter.", nameof(query))
            }
        };

    private static void AddChecked(IDictionary<string, string> data, string name, bool value)
    {
        if (value)
        {
            data[name] = "1";
        }
    }

    private static AccountGender ParseGender(string value) => value switch
    {
        "1" => AccountGender.Male,
        "2" => AccountGender.Female,
        _ => AccountGender.Unspecified
    };

    private static List<long> ValidateIds(IEnumerable<long>? values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        var result = values.Distinct().ToList();
        if (result.Count == 0)
        {
            throw new ArgumentException("At least one identifier is required.", parameterName);
        }

        if (result.Any(value => value <= 0))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return result;
    }

    private static List<KeyValuePair<string, string>> CreateRepeatedForm(
        string repeatedName,
        IEnumerable<long> values,
        params (string Name, string Value)[] fields)
    {
        var result = values.Select(value => KeyValuePair.Create(
            repeatedName,
            value.ToString(CultureInfo.InvariantCulture))).ToList();
        result.AddRange(fields.Select(field => KeyValuePair.Create(field.Name, field.Value)));
        return result;
    }

    private static void ValidateDifferentFolders(long sourceFolderId, long destinationFolderId)
    {
        ValidatePositive(sourceFolderId, nameof(sourceFolderId));
        ValidatePositive(destinationFolderId, nameof(destinationFolderId));
        if (sourceFolderId == destinationFolderId)
        {
            throw new ArgumentException("Source and destination folders must be different.", nameof(destinationFolderId));
        }
    }

    private static int ParseOptionalCount(JsonElement value, int fallback) => value.ValueKind switch
    {
        JsonValueKind.Number when value.TryGetInt32(out var count) => count,
        JsonValueKind.String when int.TryParse(
            value.GetString(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var count) => count,
        JsonValueKind.Null or JsonValueKind.Undefined => fallback,
        _ => throw new ParseException("The website returned an invalid bookmark move count.")
    };
}
