using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace HdRezka.Observability;

internal static class Telemetry
{
    private const string InstrumentationVersion = "0.1.0";

    private static readonly ActivitySource Activities = new(
        Diagnostics.ActivitySourceName,
        InstrumentationVersion);
    private static readonly Meter Meter = new(
        Diagnostics.MeterName,
        InstrumentationVersion);
    private static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "hdrezka.http.request.duration",
        "s",
        "Time spent waiting for HTTP response headers");
    private static readonly Histogram<double> BodyReadDuration = Meter.CreateHistogram<double>(
        "hdrezka.http.response.body.duration",
        "s",
        "Time spent reading and decoding the HTTP response body");
    private static readonly Histogram<long> BodySize = Meter.CreateHistogram<long>(
        "hdrezka.http.response.body.size",
        "By",
        "Size of the decoded response body");
    private static readonly Histogram<double> ParseDuration = Meter.CreateHistogram<double>(
        "hdrezka.response.parse.duration",
        "s",
        "Time spent parsing an HTML or JSON response");
    private static readonly Counter<long> CacheRequests = Meter.CreateCounter<long>(
        "hdrezka.cache.request.count",
        "{request}",
        "Cache and active-request sharing outcomes");

    public static Activity? StartHttpRequest(HttpMethod method, Uri uri)
    {
        if (!Activities.HasListeners())
        {
            return null;
        }

        var activity = Activities.StartActivity("HttpRequest", ActivityKind.Client);
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag("http.request.method", method.Method);
            activity.SetTag("server.address", uri.Host);
            activity.SetTag("url.scheme", uri.Scheme);
            activity.SetTag("url.path", uri.AbsolutePath);
        }

        return activity;
    }

    public static void RequestCompleted(
        HttpMethod method,
        TimeSpan requestDuration,
        TimeSpan bodyDuration,
        long bodySize)
    {
        var tags = new TagList
        {
            { "http.request.method", method.Method },
            { "outcome", "success" }
        };
        RequestDuration.Record(requestDuration.TotalSeconds, tags);
        BodyReadDuration.Record(bodyDuration.TotalSeconds, tags);
        BodySize.Record(bodySize, tags);
    }

    public static void RequestFailed(HttpMethod method, TimeSpan requestDuration, Exception exception)
    {
        var tags = new TagList
        {
            { "http.request.method", method.Method },
            { "outcome", "failure" },
            { "error.type", exception.GetType().Name }
        };
        RequestDuration.Record(requestDuration.TotalSeconds, tags);
    }

    public static void ParseCompleted(string format, TimeSpan duration, bool succeeded)
    {
        var tags = new TagList
        {
            { "hdrezka.response.format", format },
            { "outcome", succeeded ? "success" : "failure" }
        };
        ParseDuration.Record(duration.TotalSeconds, tags);
    }

    public static void CacheRequest(string outcome)
    {
        var tags = new TagList { { "outcome", outcome } };
        CacheRequests.Add(1, tags);
    }

    public static void ActivitySucceeded(Activity? activity) =>
        activity?.SetStatus(ActivityStatusCode.Ok);

    public static void ActivityFailed(Activity? activity, Exception exception)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag("error.type", exception.GetType().FullName);
        activity.AddEvent(new ActivityEvent(
            "exception",
            tags: new ActivityTagsCollection
            {
                ["exception.type"] = exception.GetType().FullName,
                ["exception.message"] = exception.Message,
                ["exception.stacktrace"] = exception.ToString()
            }));
    }
}
