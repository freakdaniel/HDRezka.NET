namespace HdRezka;

/// <summary>
/// Provides stable names for subscribing to HDRezka.NET tracing and metrics
/// </summary>
public static class Diagnostics
{
    /// <summary>
    /// Gets the <see cref="System.Diagnostics.ActivitySource"/> name emitted by the library
    /// </summary>
    public const string ActivitySourceName = "HDRezka.NET";

    /// <summary>
    /// Gets the <see cref="System.Diagnostics.Metrics.Meter"/> name emitted by the library
    /// </summary>
    public const string MeterName = "HDRezka.NET";
}
