namespace HdRezka;

/// <summary>
/// Holds video URLs grouped by quality together with translator and subtitle information
/// </summary>
public sealed class MediaStream
{
    private readonly Dictionary<string, StreamQuality> _qualities =
        new(StringComparer.OrdinalIgnoreCase);

    internal MediaStream(
        int? season,
        int? episode,
        string name,
        int translatorId,
        Subtitles subtitles,
        string? defaultQuality,
        string? defaultSubtitle,
        Uri? thumbnailPreview,
        bool isPremiumContent,
        AccountTier accountTier)
    {
        Season = season;
        Episode = episode;
        Name = name;
        TranslatorId = translatorId;
        Subtitles = subtitles;
        DefaultQuality = defaultQuality;
        DefaultSubtitle = defaultSubtitle;
        ThumbnailPreview = thumbnailPreview;
        IsPremiumContent = isPremiumContent;
        AccountTier = accountTier;
    }

    /// <summary>
    /// Gets the season number for a series stream
    /// </summary>
    /// <value>
    /// Season number, or <see langword="null"/> for a movie
    /// </value>
    public int? Season { get; }

    /// <summary>
    /// Gets the episode number for a series stream
    /// </summary>
    /// <value>
    /// Episode number, or <see langword="null"/> for a movie
    /// </value>
    public int? Episode { get; }

    /// <summary>
    /// Gets the media title associated with this stream
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the translator identifier used to request this stream
    /// </summary>
    public int TranslatorId { get; }

    /// <summary>
    /// Gets subtitles returned with this stream
    /// </summary>
    public Subtitles Subtitles { get; }

    /// <summary>
    /// Gets the quality selected by the website player
    /// </summary>
    /// <value>
    /// Quality label, or <see langword="null"/> when the response does not specify one
    /// </value>
    public string? DefaultQuality { get; }

    /// <summary>
    /// Gets the subtitle language selected by the website player
    /// </summary>
    /// <value>
    /// Language code or title, or <see langword="null"/> when subtitles are disabled or unspecified
    /// </value>
    public string? DefaultSubtitle { get; }

    /// <summary>
    /// Gets the image URL used by the website for timeline previews
    /// </summary>
    /// <value>
    /// Absolute preview image URL, or <see langword="null"/> when previews are unavailable
    /// </value>
    public Uri? ThumbnailPreview { get; }

    /// <summary>
    /// Gets whether the website marks the stream as available only with premium access
    /// </summary>
    public bool IsPremiumContent { get; }

    /// <summary>
    /// Gets the subscription tier used to determine which stream URLs are available
    /// </summary>
    public AccountTier AccountTier { get; }

    /// <summary>
    /// Gets every quality returned by the website including unavailable Premium qualities
    /// </summary>
    /// <value>
    /// Case-insensitive snapshot indexed by the cleaned quality label
    /// </value>
    public IReadOnlyDictionary<string, StreamQuality> Qualities =>
        new Dictionary<string, StreamQuality>(_qualities, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets available video URLs indexed by the quality label returned by the website
    /// </summary>
    /// <value>
    /// Case-insensitive snapshot that excludes Premium qualities unavailable to the current account
    /// </value>
    public IReadOnlyDictionary<string, IReadOnlyList<Uri>> Videos =>
        _qualities.Values
            .Where(quality => quality.IsAvailable)
            .ToDictionary(
                quality => quality.Name,
                quality => quality.Urls,
                StringComparer.OrdinalIgnoreCase);

    internal void AddQuality(
        string name,
        bool requiresPremium,
        IReadOnlyList<Uri> urls)
    {
        var isAvailable = !requiresPremium || AccountTier == AccountTier.Premium;
        _qualities[name] = new StreamQuality(
            name,
            requiresPremium,
            isAvailable,
            isAvailable ? urls.ToArray() : []);
    }

    /// <summary>
    /// Finds the first quality label containing the requested resolution without case sensitivity
    /// </summary>
    /// <param name="resolution">
    /// Full quality label or a fragment such as <c>720</c> or <c>Ultra</c>
    /// </param>
    /// <returns>
    /// Primary and fallback video URLs stored for the matching quality
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="resolution"/> is empty, contains only whitespace, or does not match an available quality
    /// </exception>
    /// <exception cref="PremiumRequiredException">
    /// The matching quality requires Premium and the current account does not have confirmed Premium access
    /// </exception>
    public IReadOnlyList<Uri> GetUrls(string resolution)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resolution);

        var match = _qualities.Values.FirstOrDefault(
            quality => quality.Name.Contains(resolution, StringComparison.OrdinalIgnoreCase));
        if (match is { RequiresPremium: true, IsAvailable: false })
        {
            throw new PremiumRequiredException(PremiumFeature.Quality, match.Name);
        }

        return match?.Urls ??
            throw new ArgumentException($"Resolution \"{resolution}\" is not defined.", nameof(resolution));
    }

    /// <summary>
    /// Returns a readable representation containing available quality labels
    /// </summary>
    /// <returns>
    /// Text in the form <c>MediaStream(720p, 1080p)</c>
    /// </returns>
    public override string ToString() =>
        $"MediaStream({string.Join(", ", _qualities.Keys)})";
}

/// <summary>
/// Describes one video quality and whether its URLs are available to the current account
/// </summary>
/// <param name="Name">
/// Clean quality label shown by the website player
/// </param>
/// <param name="RequiresPremium">
/// <see langword="true"/> when the website marks this quality as Premium-only
/// </param>
/// <param name="IsAvailable">
/// <see langword="true"/> when the current account may access the URLs
/// </param>
/// <param name="Urls">
/// Primary and fallback video URLs, or an empty list when the quality is unavailable
/// </param>
public sealed record StreamQuality(
    string Name,
    bool RequiresPremium,
    bool IsAvailable,
    IReadOnlyList<Uri> Urls);

/// <summary>
/// Holds subtitle tracks indexed by language code
/// </summary>
public sealed class Subtitles
{
    internal Subtitles(IReadOnlyDictionary<string, Subtitle> items)
    {
        Items = items;
    }

    /// <summary>
    /// Gets subtitle tracks indexed by language code without case sensitivity
    /// </summary>
    public IReadOnlyDictionary<string, Subtitle> Items { get; }

    /// <summary>
    /// Gets language codes in the same order as <see cref="Items"/>
    /// </summary>
    public IReadOnlyList<string> Languages => [.. Items.Keys];

    /// <summary>
    /// Finds a subtitle URL by language code or displayed title
    /// </summary>
    /// <param name="language">
    /// Language code or title, or <see langword="null"/> when no subtitle should be selected
    /// </param>
    /// <returns>
    /// Matching subtitle URL, or <see langword="null"/> when <paramref name="language"/> is <see langword="null"/>
    /// </returns>
    /// <exception cref="ArgumentException">
    /// No subtitle matches <paramref name="language"/>
    /// </exception>
    public Uri? GetUrl(string? language = null)
    {
        if (language is null)
        {
            return null;
        }

        if (Items.TryGetValue(language, out var subtitle))
        {
            return subtitle.Url;
        }

        subtitle = Items.Values.FirstOrDefault(
            item => string.Equals(item.Title, language, StringComparison.OrdinalIgnoreCase));

        return subtitle?.Url ??
            throw new ArgumentException($"Subtitles \"{language}\" are not defined.", nameof(language));
    }

    /// <summary>
    /// Gets a subtitle URL by its zero-based position
    /// </summary>
    /// <param name="index">
    /// Zero-based position in <see cref="Items"/>
    /// </param>
    /// <returns>
    /// Subtitle URL at the requested position
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or outside the available subtitle range
    /// </exception>
    public Uri GetUrl(int index)
    {
        if (index < 0 || index >= Items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return Items.ElementAt(index).Value.Url;
    }

    /// <summary>
    /// Returns available language codes separated by commas
    /// </summary>
    /// <returns>
    /// Comma-separated language codes or an empty string when no subtitles are available
    /// </returns>
    public override string ToString() => string.Join(", ", Languages);
}

/// <summary>
/// Describes one subtitle track
/// </summary>
/// <param name="Language">
/// Language code used as the key in <see cref="Subtitles.Items"/>
/// </param>
/// <param name="Title">
/// Human-readable language title returned by the website
/// </param>
/// <param name="Url">
/// Absolute subtitle file URL
/// </param>
public sealed record Subtitle(string Language, string Title, Uri Url);
