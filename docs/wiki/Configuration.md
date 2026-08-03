# Configuration

Configure a session with `ClientOptions` before constructing `Client`,
`SearchClient`, or standalone `Media`

```csharp
var options = new ClientOptions();
options.Headers["X-Custom-Header"] = "value";
options.Cookies["preference"] = "value";

using var client = new Client(
    "https://your-mirror.example",
    options);
```

The client clones the supplied options, so later changes to the original object do
not affect the client; use `client.Options` to update settings for subsequent
requests

## Concurrent requests

Bulk operations use limited asynchronous concurrency:

```csharp
var options = new ClientOptions
{
    MaxConcurrentRequests = 4
};
```

The limit applies to bookmark folders, full searches, translator catalogs, and
whole-season stream loading. Increasing it can reduce latency on a fast mirror
but also increases the chance of throttling. Dedicated worker threads are not
needed because HTTP waiting is asynchronous

## Response caching and request sharing

Identical safe reads share one active request within a client even when
response retention is disabled. Retention is opt-in:

```csharp
var options = new ClientOptions
{
    ResponseCacheDuration = TimeSpan.FromSeconds(15),
    MaxCachedResponses = 128,
    SecurityTokenCacheDuration = TimeSpan.FromSeconds(30)
};
```

`ResponseCacheDuration` defaults to zero, which keeps active-request sharing
but does not retain a completed response. `MaxCachedResponses` defaults to 128
and bounds completed entries. Only explicitly safe catalog, collection, media,
and account reads participate. Failed requests are removed immediately

Cache keys include the request and current session-cookie state. Changing
authentication cookies therefore cannot reuse a response from another session.
Successful mutations invalidate potentially stale reads. Cancellation stops
only the caller's wait; it does not cancel shared work that another caller is
still awaiting

Protected account operations reuse only the parsed security token, not the
whole settings page. `SecurityTokenCacheDuration` defaults to 30 seconds. A
rejected token is refreshed and retried once only when the website explicitly
reports a token or session-security error

`TimeProvider` defaults to `TimeProvider.System` and can be replaced when an
application needs a controlled clock for cache expiration tests

## Diagnostics

Subscribe to the BCL `ActivitySource` and `Meter` names exposed by
`Diagnostics.ActivitySourceName` and `Diagnostics.MeterName`.
Both currently use `HDRezka.NET`

The library emits these instruments:

- `hdrezka.http.request.duration`
- `hdrezka.http.response.body.duration`
- `hdrezka.http.response.body.size`
- `hdrezka.response.parse.duration`
- `hdrezka.cache.request.count`

HTTP activities include the method, server, scheme, and URL path. Query values
are deliberately omitted

## Headers

`Headers` is a case-insensitive mutable dictionary with a
browser-like `User-Agent`:

```csharp
var options = new ClientOptions();
options.Headers["User-Agent"] = "MyApplication/1.0";
options.Headers["Accept-Language"] = "en-US,en;q=0.9";
```

Invalid or restricted header combinations can result in
`InvalidOperationException` when a request is created

## Cookies

New options contain the website preference cookie `hdmbbs=1`

```csharp
options.Cookies["hdmbbs"] = "1";
```

Authentication responses keep the options dictionary synchronized with the
session cookie container; for persistent authentication values, use
`AuthenticationCookies.Create`; see [Authentication](Authentication)

## Proxy

```csharp
using System.Net;

var options = new ClientOptions
{
    Proxy = new WebProxy("http://127.0.0.1:8080")
};

using var client = new Client(
    "https://your-mirror.example",
    options);
```

`Proxy` applies only when the library creates the underlying `HttpClient`

## Custom HttpClient

```csharp
using var handler = new HttpClientHandler
{
    Proxy = new WebProxy("http://127.0.0.1:8080"),
    UseProxy = true
};

using var httpClient = new HttpClient(handler);
using var client = new Client(
    "https://your-mirror.example",
    options: null,
    httpClient);
```

When supplied by the caller, `HttpClient` and its handler remain owned by the
caller and are not disposed by the library, so configure proxy behavior on the
handler because `ClientOptions.Proxy` is ignored in this case

## Translator priority

Defaults:

- preferred: `56`, `105`, `111`
- non-preferred: `238`

Preferred entries are tried first in list order, neutral entries follow in
website order, and non-preferred entries are placed last

Configure them before creating the client:

```csharp
var options = new ClientOptions();

options.PreferredTranslators.Clear();
options.PreferredTranslators.Add(111);
options.PreferredTranslators.Add(56);

options.NonPreferredTranslators.Clear();
options.NonPreferredTranslators.Add(238);
options.NonPreferredTranslators.Add(999);
```

Or adjust a loaded media instance for subsequent automatic selections:

```csharp
media.PreferredTranslators.Clear();
media.PreferredTranslators.Add(111);

media.NonPreferredTranslators.Add(999);
```

Methods such as `GetStreamAsync` also accept per-call `preferred` and
`nonPreferred` lists
