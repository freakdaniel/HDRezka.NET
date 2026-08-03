using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace HdRezka.Tests;

public sealed class DiagnosticsTests
{
    [Fact]
    public async Task Requests_EmitSanitizedActivitiesAndStageMetrics()
    {
        var activities = new ConcurrentBag<Activity>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == Diagnostics.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(activityListener);

        var measurements = new ConcurrentBag<string>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == Diagnostics.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<double>(
            (instrument, _, _, _) => measurements.Add(instrument.Name));
        meterListener.SetMeasurementEventCallback<long>(
            (instrument, _, _, _) => measurements.Add(instrument.Name));
        meterListener.Start();

        using var httpClient = new HttpClient(new StubHttpHandler((_, _) =>
            Task.FromResult(StubHttpHandler.Html(PageHtml))));
        using var client = new Client("https://mirror.test", httpClient: httpClient);

        using var media = await client.GetAsync("/films/987654-diagnostics.html?secret=value");

        var activity = Assert.Single(
            activities,
            item => Equals(item.GetTagItem("url.path"), "/films/987654-diagnostics.html"));
        Assert.Equal("/films/987654-diagnostics.html", activity.GetTagItem("url.path"));
        Assert.Null(activity.GetTagItem("url.query"));
        Assert.Contains("hdrezka.http.request.duration", measurements);
        Assert.Contains("hdrezka.http.response.body.duration", measurements);
        Assert.Contains("hdrezka.http.response.body.size", measurements);
        Assert.Contains("hdrezka.response.parse.duration", measurements);
        Assert.Contains("hdrezka.cache.request.count", measurements);
    }

    private const string PageHtml = """
        <html>
        <head><title>Movie</title><meta property="og:type" content="video.movie"></head>
        <body>
          <input id="post_id" value="42">
          <h1 class="b-post__title">Title</h1>
          <div class="b-sidecover"><a href="/hq.jpg"><img src="/cover.jpg"></a></div>
          <ul id="translators-list"><li data-translator_id="56">Dub</li></ul>
        </body>
        </html>
        """;
}
