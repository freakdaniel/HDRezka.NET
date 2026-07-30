using System.Net;

namespace HdRezka;

/// <summary>
/// Configures request headers, cookies, proxy use, and automatic translator selection
/// </summary>
public sealed class ClientOptions
{
    private int _maxConcurrentRequests = 4;

    /// <summary>
    /// Gets the browser-like user agent included in new option instances
    /// </summary>
    public const string DefaultUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    /// <summary>
    /// Creates settings with the default user agent, website cookies, and translator priorities
    /// </summary>
    public ClientOptions()
    {
    }

    /// <summary>
    /// Gets request headers copied to every outgoing request
    /// </summary>
    /// <value>
    /// Case-insensitive mutable dictionary containing <c>User-Agent</c> by default
    /// </value>
    public IDictionary<string, string> Headers { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["User-Agent"] = DefaultUserAgent
        };

    /// <summary>
    /// Gets cookies stored and sent by the client
    /// </summary>
    /// <value>
    /// Mutable dictionary initially containing the website preference cookie <c>hdmbbs=1</c>.
    /// Authentication responses update this dictionary with returned cookie values
    /// </value>
    public IDictionary<string, string> Cookies { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["hdmbbs"] = "1"
        };

    /// <summary>
    /// Gets or sets the proxy used when the library creates its own <see cref="HttpClient"/>
    /// </summary>
    /// <value>
    /// Proxy configuration, or <see langword="null"/> for the platform default.
    /// This setting is ignored when an <see cref="HttpClient"/> is supplied by the caller
    /// </value>
    public IWebProxy? Proxy { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of requests used by one bulk operation
    /// </summary>
    /// <value>
    /// Positive request limit with a default value of <c>4</c>
    /// </value>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The assigned value is less than one
    /// </exception>
    public int MaxConcurrentRequests
    {
        get => _maxConcurrentRequests;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maxConcurrentRequests = value;
        }
    }

    /// <summary>
    /// Gets translator identifiers placed first during automatic selection
    /// </summary>
    /// <value>
    /// Mutable identifiers ordered from highest to lowest priority with defaults <c>56</c>, <c>105</c>, and <c>111</c>
    /// </value>
    public IList<int> PreferredTranslators { get; } = [56, 105, 111];

    /// <summary>
    /// Gets translator identifiers placed after preferred and neutral choices
    /// </summary>
    /// <value>
    /// Mutable identifiers ordered from earlier to later fallback with <c>238</c> included by default
    /// </value>
    public IList<int> NonPreferredTranslators { get; } = [238];

    internal ClientOptions Clone()
    {
        var clone = new ClientOptions
        {
            Proxy = Proxy,
            MaxConcurrentRequests = MaxConcurrentRequests
        };
        clone.Headers.Clear();
        foreach (var pair in Headers)
        {
            clone.Headers[pair.Key] = pair.Value;
        }

        clone.Cookies.Clear();
        foreach (var pair in Cookies)
        {
            clone.Cookies[pair.Key] = pair.Value;
        }

        clone.PreferredTranslators.Clear();
        foreach (var id in PreferredTranslators)
        {
            clone.PreferredTranslators.Add(id);
        }

        clone.NonPreferredTranslators.Clear();
        foreach (var id in NonPreferredTranslators)
        {
            clone.NonPreferredTranslators.Add(id);
        }

        return clone;
    }
}
