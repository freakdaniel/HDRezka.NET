namespace HdRezka.Http;

internal static class AsyncUtilities
{
    public static async Task<TResult[]> SelectAsync<TSource, TResult>(
        IEnumerable<TSource> source,
        int maximumConcurrency,
        Func<TSource, CancellationToken, Task<TResult>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumConcurrency, 1);

        using var gate = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
        var tasks = source.Select(
            async item =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    return await selector(item, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    gate.Release();
                }
            });
        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
