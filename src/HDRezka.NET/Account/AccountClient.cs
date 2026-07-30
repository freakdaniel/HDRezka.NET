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

        var tasks = root.Folders
            .Select(folder => LoadBookmarkFolderAsync(folder, root, cancellationToken))
            .ToArray();
        return await Task.WhenAll(tasks).ConfigureAwait(false);
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
}
