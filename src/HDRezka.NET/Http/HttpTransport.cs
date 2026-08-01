using System.Net;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using HdRezka.Abstractions;

namespace HdRezka.Http;

internal sealed class HttpTransport : IHttpTransport
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly object _cookieLock = new();
    private readonly CookieContainer _cookieContainer = new();

    public HttpTransport(ClientOptions options, HttpClient? client = null)
    {
        Options = options;
        if (client is not null)
        {
            _client = client;
            return;
        }

        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false
        };

        if (options.Proxy is not null)
        {
            handler.Proxy = options.Proxy;
            handler.UseProxy = true;
        }

        _client = new HttpClient(handler, disposeHandler: true);
        _ownsClient = true;
    }

    public ClientOptions Options { get; }

    public async Task<string> GetStringAsync(
        Uri uri,
        IReadOnlyDictionary<string, string?>? query = null,
        CancellationToken cancellationToken = default)
    {
        if (query is not null)
        {
            uri = AddQuery(uri, query);
        }

        using var request = CreateRequest(HttpMethod.Get, uri);
        using var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        return await ReadResponseAsync(response, uri, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> PostFormAsync(
        Uri uri,
        IEnumerable<KeyValuePair<string, string>> data,
        CancellationToken cancellationToken = default,
        Uri? referrer = null)
    {
        using var request = CreateRequest(HttpMethod.Post, uri);
        request.Headers.Referrer = referrer;
        request.Content = new FormUrlEncodedContent(data);
        using var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        return await ReadResponseAsync(response, uri, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> PostFormJsonAsync<T>(
        Uri uri,
        IEnumerable<KeyValuePair<string, string>> data,
        CancellationToken cancellationToken = default,
        Uri? referrer = null)
    {
        var json = await PostFormAsync(uri, data, cancellationToken, referrer)
            .ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ??
            throw new ParseException("The server returned an empty JSON response.");
    }

    public async Task<T> PostMultipartJsonAsync<T>(
        Uri uri,
        IReadOnlyDictionary<string, string> fields,
        string fileFieldName,
        ReadOnlyMemory<byte> file,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default,
        Uri? referrer = null)
    {
        using var content = new MultipartFormDataContent();
        foreach (var field in fields)
        {
            content.Add(new StringContent(field.Value), field.Key);
        }

        var fileContent = new ByteArrayContent(file.ToArray());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        content.Add(fileContent, fileFieldName, fileName);
        using var request = CreateRequest(HttpMethod.Post, uri);
        request.Headers.Referrer = referrer;
        request.Content = content;
        using var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        var json = await ReadResponseAsync(response, uri, cancellationToken)
            .ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ??
            throw new ParseException("The server returned an empty JSON response.");
    }

    public async Task<T> GetJsonAsync<T>(
        Uri uri,
        IReadOnlyDictionary<string, string?>? query = null,
        CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync(uri, query, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ??
            throw new ParseException("The server returned an empty JSON response.");
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, Uri uri)
    {
        var request = new HttpRequestMessage(method, uri);
        foreach (var header in Options.Headers)
        {
            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                throw new InvalidOperationException($"Header \"{header.Key}\" cannot be added to a request.");
            }
        }

        lock (_cookieLock)
        {
            SeedCookies(uri);
            var cookieHeader = _cookieContainer.GetCookieHeader(uri);
            if (!string.IsNullOrWhiteSpace(cookieHeader))
            {
                request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
            }
        }

        return request;
    }

    private async Task<string> ReadResponseAsync(
        HttpResponseMessage response,
        Uri requestUri,
        CancellationToken cancellationToken)
    {
        CaptureCookies(response, requestUri);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpException(response.StatusCode, response.ReasonPhrase);
        }

        return await ReadContentAsStringAsync(response.Content, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<string> ReadContentAsStringAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentEncoding.Count == 0)
        {
            return await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        var bytes = await content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        foreach (var encoding in content.Headers.ContentEncoding.Reverse())
        {
            await using var input = new MemoryStream(bytes);
            await using Stream decompressor = encoding.ToLowerInvariant() switch
            {
                "gzip" => new GZipStream(input, CompressionMode.Decompress),
                "deflate" => new DeflateStream(input, CompressionMode.Decompress),
                "br" => new BrotliStream(input, CompressionMode.Decompress),
                _ => throw new ParseException(
                    $"The server used unsupported content encoding \"{encoding}\".")
            };
            await using var output = new MemoryStream();
            await decompressor.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            bytes = output.ToArray();
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private void CaptureCookies(HttpResponseMessage response, Uri requestUri)
    {
        var responseUri = response.RequestMessage?.RequestUri ?? requestUri;
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return;
        }

        lock (_cookieLock)
        {
            foreach (var value in values)
            {
                try
                {
                    _cookieContainer.SetCookies(responseUri, value);
                }
                catch (CookieException exception)
                {
                    throw new ParseException(
                        "The server returned an invalid Set-Cookie header.",
                        exception);
                }
            }

            SynchronizeOptionCookies(responseUri);
        }
    }

    public IReadOnlyCollection<string> GetCookieNames(Uri uri)
    {
        lock (_cookieLock)
        {
            SeedCookies(uri);
            return _cookieContainer
                .GetCookies(uri)
                .Cast<Cookie>()
                .Select(cookie => cookie.Name)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
    }

    public void ClearCookies(Uri uri, IEnumerable<string> names)
    {
        var namesToRemove = names.ToHashSet(StringComparer.Ordinal);
        lock (_cookieLock)
        {
            foreach (var name in namesToRemove)
            {
                Options.Cookies.Remove(name);
            }

            foreach (Cookie cookie in _cookieContainer.GetCookies(uri))
            {
                if (namesToRemove.Contains(cookie.Name))
                {
                    cookie.Expired = true;
                }
            }

            SynchronizeOptionCookies(uri);
        }
    }

    private void SeedCookies(Uri uri)
    {
        foreach (var pair in Options.Cookies)
        {
            _cookieContainer.Add(
                uri,
                new Cookie(pair.Key, pair.Value, "/", uri.Host));
        }
    }

    private void SynchronizeOptionCookies(Uri uri)
    {
        var current = _cookieContainer
            .GetCookies(uri)
            .Cast<Cookie>()
            .ToDictionary(cookie => cookie.Name, cookie => cookie.Value, StringComparer.Ordinal);

        Options.Cookies.Clear();
        foreach (var pair in current)
        {
            Options.Cookies[pair.Key] = pair.Value;
        }
    }

    private static Uri AddQuery(Uri uri, IReadOnlyDictionary<string, string?> values)
    {
        var query = string.Join(
            "&",
            values
                .Where(pair => pair.Value is not null)
                .Select(pair =>
                    $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));

        var builder = new UriBuilder(uri)
        {
            Query = string.IsNullOrEmpty(uri.Query)
                ? query
                : $"{uri.Query.TrimStart('?')}&{query}"
        };
        return builder.Uri;
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }
}
