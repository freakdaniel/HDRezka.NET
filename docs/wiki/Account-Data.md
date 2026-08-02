# Account data

Account data is exposed through `Client.Account` and uses the cookies stored by
the same client session. Log in before calling these methods:

```csharp
using HdRezka;

using var client = new Client("https://your-mirror.example");
await client.LoginAsync("mail@example.com", "password", rememberMe: true);
```

## Profile

```csharp
var profile = await client.Account.GetProfileAsync();

Console.WriteLine(profile.Id);
Console.WriteLine(profile.Username);
Console.WriteLine(profile.Email);
Console.WriteLine(profile.AvatarUrl);
Console.WriteLine(profile.Tier);
Console.WriteLine(profile.IsPremium);
Console.WriteLine(profile.ContinueWatchingCount);
Console.WriteLine(profile.Gender);
```

`Email` and `AvatarUrl` are nullable because a compatible mirror may omit them.
`Tier` contains the subscription tier detected from account data, while
`IsPremium` is a convenience property for checking `AccountTier.Premium`.

```csharp
var settings = await client.Account.GetSettingsAsync(cancellationToken);
await client.Account.UpdateSettingsAsync(
    settings with { Gender = AccountGender.Female },
    cancellationToken);

var preferences = await client.Account.GetPlaybackPreferencesAsync(cancellationToken);
await client.Account.UpdatePlaybackPreferencesAsync(
    preferences with { AutoSwitchEpisodes = false },
    cancellationToken);
```

## Change password

```csharp
var result = await client.Account.ChangePasswordAsync(
    currentPassword: "current-password",
    newPassword: "new-password",
    cancellationToken: cancellationToken);

Console.WriteLine(result.Message);
```

The website requires the current password, verifies that both submitted copies
of the new password match, and rejects passwords shorter than eight characters.
The library sends the confirmation copy automatically and never stores either
password in account models or options.

## Change avatar

```csharp
await using var image = File.OpenRead("avatar.png");
var result = await client.Account.SetAvatarAsync(
    image,
    "avatar.png",
    cancellationToken: cancellationToken);

Console.WriteLine(result.AvatarUrl);
```

The website first uploads a temporary image and then creates a 60 by 60 pixel
avatar from a square crop. PNG and JPEG uploads are confirmed, and the source
image must be at least 60 by 60 pixels. No client-side maximum file size is
published by the website, so server validation remains authoritative.

Without an explicit crop, the largest centered square is used. Custom crop
coordinates refer to the original image:

```csharp
var crop = new AvatarCrop(X: 100, Y: 40, Size: 600);
var result = await client.Account.SetAvatarAsync(
    image,
    "avatar.jpg",
    crop,
    cancellationToken);
```

Remove the current avatar while preserving the existing email and gender
settings:

```csharp
await client.Account.RemoveAvatarAsync(cancellationToken);
```

## Continue watching

```csharp
var entries = await client.Account.GetContinueWatchingAsync();

foreach (var entry in entries)
{
    Console.WriteLine(
        $"{entry.Title}: S{entry.Season}E{entry.Episode}, {entry.Translator}");
}
```

Each `ContinueWatchingEntry` includes the saved position identifier, media URL,
cover, category, date label, details shown by the website, playback
information, watched state, and remaining episode count. Season, episode,
translator, parsed date, and remaining count are nullable when the website
does not provide recognizable values.

The method loads the complete `/continue/` page because the website does not
expose pagination for this list.

## Playback synchronization

The website stores the latest media, translator, season, episode, current
position, and complete duration for the authenticated account

```csharp
using var media = await client.GetAsync(
    "/series/drama/66689-title.html");
var stream = await media.GetStreamAsync(season: 1, episode: 4);

await client.Account.SavePlaybackProgressAsync(
    new PlaybackProgress(
        media.Id,
        stream.TranslatorId,
        stream.Season,
        stream.Episode,
        Position: TimeSpan.FromMinutes(18),
        Duration: TimeSpan.FromMinutes(52)));
```

`Position` and `Duration` must be supplied together or both omitted

Omitting both values still synchronizes the latest media, translator, season,
and episode

HDRezka.NET resolves streams but does not contain a media player, so it cannot
observe playback time automatically

Call the method from the application when playback starts, pauses, seeks, or
closes, and periodically during long playback when more frequent synchronization
is needed

## Continue-watching changes

Mark an entry as watched or not watched using the state returned by the website

```csharp
var entry = entries[0];
var watched = await client.Account.SetContinueWatchingWatchedAsync(
    entry,
    isWatched: true);
```

No request is sent when the entry already has the requested state

The returned record is a new immutable snapshot with the updated `IsWatched`
value

Remove a saved position from the list by its website identifier

```csharp
await client.Account.RemoveContinueWatchingAsync(entry.Id);
```

## Bookmarks

```csharp
var folders = await client.Account.GetBookmarksAsync();

foreach (var folder in folders)
{
    Console.WriteLine($"{folder.Name}: {folder.ItemCount}");

    foreach (var item in folder.Items)
    {
        Console.WriteLine($"  {item.Title}: {item.Url}");
    }
}
```

The result preserves the website folder order. The method loads the root
bookmark page and then loads the remaining user-created folders concurrently.
Every bookmarked media card uses the same `CatalogItem` model as catalog and
collection pages.

Apply the website sort and category filter to every folder:

```csharp
var series = await client.Account.GetBookmarksAsync(
    new BookmarkQuery(BookmarkSort.Popular, MediaCategory.Series),
    cancellationToken);
```

Create and delete user sections through the same authenticated session

```csharp
var folder = await client.Account.CreateBookmarkFolderAsync("Watch later");

Console.WriteLine(folder.Id);
Console.WriteLine(folder.Name);

await client.Account.DeleteBookmarkFolderAsync(folder.Id);
```

Folders can be renamed and reordered. Selected media can be removed or moved,
and a complete folder can be merged into another one:

```csharp
folder = await client.Account.RenameBookmarkFolderAsync(
    folder,
    "Favorites",
    cancellationToken);
await client.Account.SortBookmarkFoldersAsync([folder.Id, another.Id], cancellationToken);
await client.Account.RemoveBookmarksAsync(folder.Id, [123, 456], cancellationToken);
await client.Account.MoveBookmarksAsync(folder.Id, another.Id, [789], cancellationToken);
await client.Account.MoveBookmarkFolderAsync(folder.Id, another.Id, cancellationToken);
```

## Premium metadata

Premium payment information is read-only and never starts checkout:

```csharp
var history = await client.Account.GetPaymentHistoryAsync(cancellationToken);
var offers = await client.Account.GetPremiumOffersAsync(
    currency: "eu",
    cancellationToken);
```

Deleting a section also deletes every bookmark stored inside it

The loaded media page exposes its current section identifiers and can add or
remove itself without sending a redundant request

```csharp
Console.WriteLine(string.Join(", ", media.BookmarkFolderIds));

await media.SetBookmarkAsync(
    folderId: folder.Id,
    isBookmarked: true);
```

`SetBookmarkAsync` updates `BookmarkFolderIds` after a successful request

`AccountClient.ToggleBookmarkAsync` remains available when the caller
intentionally needs the website checkbox behavior without loading a media page

## Errors and cancellation

All account methods accept a `CancellationToken`. They can throw
`LoginRequiredException` for an anonymous session, `CaptchaException` when the
website requests verification, `HttpException` for an unsuccessful response,
`ParseException` for incompatible markup, `HttpRequestException` for transport
failures, and `OperationCanceledException` when canceled.

Account-changing requests also throw `AccountOperationException` when the
website returns a readable rejection message

Profile changes additionally throw `AccountUpdateException` when the website
rejects a password, image, crop, or avatar removal
