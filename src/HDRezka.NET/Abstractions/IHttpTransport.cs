namespace HdRezka.Abstractions;

internal interface IHttpTransport : IDisposable
{
    Task<string> GetStringAsync(
        Uri uri,
        IReadOnlyDictionary<string, string?>? query = null,
        CancellationToken cancellationToken = default);

    Task<string> PostFormAsync(
        Uri uri,
        IEnumerable<KeyValuePair<string, string>> data,
        CancellationToken cancellationToken = default,
        Uri? referrer = null);

    IReadOnlyCollection<string> GetCookieNames(Uri uri);

    void ClearCookies(Uri uri, IEnumerable<string> names);
}
