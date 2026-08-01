# Error handling

Failures detected while communicating with a compatible website derive from
`ApiException`, while input validation, cancellation, and low-level platform failures
use standard .NET exceptions

## Library exceptions

| Exception | Meaning | Typical response |
| --- | --- | --- |
| `LoginRequiredException` | The requested page requires an authenticated account | Log in and retry with the same client |
| `LoginFailedException` | Credentials were rejected or the session could not be verified | Ask for new credentials; do not retry indefinitely |
| `AccountUpdateException` | A password or avatar change was rejected | Show the website validation message and keep the previous profile data |
| `CommentOperationException` | Comment creation, reply, or deletion was rejected | Check authentication, ownership, moderation, and comment rules |
| `RatingException` | An internal rating was rejected | Do not retry an account that has already voted |
| `PremiumRequiredException` | Content, a translation, or a quality is Premium-only | Offer an available alternative or require Premium |
| `CaptchaException` | The website requested interactive captcha verification | Stop automation and let the user complete verification |
| `StreamFetchException` | No usable stream or translator was returned | Try another translation or report temporary unavailability |
| `HttpException` | The website returned an unsuccessful HTTP status | Inspect `StatusCode`; retry only transient statuses |
| `ParseException` | Markup, JSON, cookies, compression, or stream data could not be read | Check for a website change and report a reproducible issue |

## Catch specific failures first

```csharp
try
{
    using var media = await client.GetAsync(path, cancellationToken);
    var stream = await media.GetStreamAsync(
        cancellationToken: cancellationToken);
}
catch (PremiumRequiredException exception)
{
    Console.WriteLine(
        $"{exception.Feature}: {exception.Name ?? "content"}");
}
catch (CaptchaException)
{
    Console.WriteLine("Interactive verification is required.");
}
catch (HttpException exception)
{
    Console.WriteLine($"HTTP status: {(int)exception.StatusCode}");
}
catch (ParseException exception)
{
    Console.WriteLine($"Website response changed: {exception.Message}");
}
catch (ApiException exception)
{
    Console.WriteLine(exception.Message);
}
```

## Standard exceptions

- `ArgumentException` — empty input, missing season or episode, unavailable
  translator, unknown quality, or invalid media path
- `ArgumentOutOfRangeException` — invalid page, subtitle position, or page
  limit
- `InvalidOperationException` — an origin is required, the media format is
  incompatible with the operation, or request configuration is invalid
- `HttpRequestException` — DNS, TLS, proxy, socket, or other transport failure
- `System.Text.Json.JsonException` — malformed JSON returned by a website
  endpoint
- `OperationCanceledException` — caller cancellation or a caller-defined
  timeout

Do not catch `OperationCanceledException` as a generic failure:

```csharp
try
{
    return await client.GetAsync(path, cancellationToken);
}
catch (OperationCanceledException)
    when (cancellationToken.IsCancellationRequested)
{
    throw;
}
```

## Retry guidance

Retry only failures likely to be transient, such as selected HTTP 429 or 5xx
responses and network interruptions; use bounded retries, exponential backoff,
and cancellation, and do not automatically retry invalid input, failed login,
Premium requirements, captcha, or a stable parsing failure

`GetSeasonStreamsAsync` already retries each failed episode once by default
See [Series and episodes](Series-and-Episodes) before adding another retry
layer around it
