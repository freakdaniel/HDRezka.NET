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
```

`Email` and `AvatarUrl` are nullable because a compatible mirror may omit them.
`Tier` contains the subscription tier detected from account data, while
`IsPremium` is a convenience property for checking `AccountTier.Premium`.

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

## Errors and cancellation

All account methods accept a `CancellationToken`. They can throw
`LoginRequiredException` for an anonymous session, `CaptchaException` when the
website requests verification, `HttpException` for an unsuccessful response,
`ParseException` for incompatible markup, `HttpRequestException` for transport
failures, and `OperationCanceledException` when canceled.
