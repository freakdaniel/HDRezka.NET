using System.Net;

namespace HdRezka;

/// <summary>
/// Base exception for errors reported or detected while communicating with the website
/// </summary>
public class ApiException : Exception
{
    /// <summary>
    /// Creates an API exception with a readable error message
    /// </summary>
    /// <param name="message">
    /// Text describing the failure
    /// </param>
    public ApiException(string message) : base(message) { }

    /// <summary>
    /// Creates an API exception with a readable error message and its original cause
    /// </summary>
    /// <param name="message">
    /// Text describing the failure
    /// </param>
    /// <param name="innerException">
    /// Exception that caused this failure
    /// </param>
    public ApiException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Indicates that the website requires an authenticated account for the requested page
/// </summary>
public sealed class LoginRequiredException : ApiException
{
    /// <summary>
    /// Creates an exception with the standard login-required message
    /// </summary>
    public LoginRequiredException() : base("Login is required to access this page.") { }
}

/// <summary>
/// Indicates that sign-in failed or the returned authentication could not be verified
/// </summary>
public sealed class LoginFailedException : ApiException
{
    /// <summary>
    /// Creates a login failure with the reason returned or detected by the client
    /// </summary>
    /// <param name="message">
    /// Text explaining why sign-in failed
    /// </param>
    public LoginFailedException(string message) : base(message) { }
}

/// <summary>
/// Indicates that an authenticated account operation was rejected by the website
/// </summary>
public sealed class AccountOperationException : ApiException
{
    /// <summary>
    /// Creates an account operation failure with the reason returned by the website
    /// </summary>
    /// <param name="message">
    /// Text explaining why the account operation failed
    /// </param>
    public AccountOperationException(string message) : base(message) { }
}

/// <summary>
/// Indicates that the website rejected an account profile change
/// </summary>
public sealed class AccountUpdateException : ApiException
{
    /// <summary>
    /// Creates an account update failure with the reason returned by the website
    /// </summary>
    /// <param name="message">
    /// Text explaining why the account change was rejected
    /// </param>
    public AccountUpdateException(string message) : base(message) { }
}

/// <summary>
/// Indicates that the website rejected creation, reply, or deletion of a comment
/// </summary>
public sealed class CommentOperationException : ApiException
{
    /// <summary>
    /// Creates a comment operation failure with the reason returned by the website
    /// </summary>
    /// <param name="message">
    /// Text explaining why the comment operation was rejected
    /// </param>
    public CommentOperationException(string message) : base(message) { }
}

/// <summary>
/// Indicates that the website rejected a media rating
/// </summary>
public sealed class RatingException : ApiException
{
    /// <summary>
    /// Creates a rating failure with the reason returned by the website
    /// </summary>
    /// <param name="message">
    /// Text explaining why the rating was rejected
    /// </param>
    public RatingException(string message) : base(message) { }
}

/// <summary>
/// Indicates that the website rejected or could not provide a requested trailer
/// </summary>
public sealed class TrailerException : ApiException
{
    /// <summary>
    /// Creates a trailer failure with the reason returned or detected by the client
    /// </summary>
    /// <param name="message">
    /// Text explaining why the trailer could not be loaded
    /// </param>
    public TrailerException(string message) : base(message) { }
}

/// <summary>
/// Identifies the Premium-protected resource requested by the caller
/// </summary>
public enum PremiumFeature
{
    /// <summary>
    /// A complete media stream protected by Premium
    /// </summary>
    Content,

    /// <summary>
    /// A translation protected by Premium
    /// </summary>
    Translation,

    /// <summary>
    /// A video quality protected by Premium
    /// </summary>
    Quality
}

/// <summary>
/// Indicates that the requested content, translation, or quality requires an active Premium subscription
/// </summary>
public sealed class PremiumRequiredException : ApiException
{
    /// <summary>
    /// Creates an exception for a Premium-protected resource
    /// </summary>
    /// <param name="feature">
    /// Kind of protected resource that was requested
    /// </param>
    /// <param name="name">
    /// Translation or quality label, or <see langword="null"/> when the whole content is protected
    /// </param>
    public PremiumRequiredException(PremiumFeature feature, string? name = null)
        : base(CreateMessage(feature, name))
    {
        Feature = feature;
        Name = name;
    }

    /// <summary>
    /// Gets the kind of Premium-protected resource that was requested
    /// </summary>
    public PremiumFeature Feature { get; }

    /// <summary>
    /// Gets the translation or quality label associated with the failure
    /// </summary>
    /// <value>
    /// Translation or quality label, or <see langword="null"/> when the whole content is protected
    /// </value>
    public string? Name { get; }

    private static string CreateMessage(PremiumFeature feature, string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? $"{feature} requires an active HDRezka Premium subscription."
            : $"{feature} \"{name}\" requires an active HDRezka Premium subscription.";
}

/// <summary>
/// Indicates that the website did not provide a usable stream
/// </summary>
public sealed class StreamFetchException : ApiException
{
    /// <summary>
    /// Creates an exception with the standard stream failure message
    /// </summary>
    public StreamFetchException() : base("Failed to fetch stream.") { }
}

/// <summary>
/// Indicates that the media page exists but the website does not currently provide a player
/// </summary>
public sealed class PlaybackUnavailableException : ApiException
{
    /// <summary>
    /// Creates an exception containing the availability state reported by the media page
    /// </summary>
    /// <param name="playback">
    /// Unavailable playback state and optional website reason
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="playback"/> is <see langword="null"/>
    /// </exception>
    public PlaybackUnavailableException(PlaybackState playback)
        : base(CreateMessage(playback))
    {
        Playback = playback;
    }

    /// <summary>
    /// Gets the unavailable playback state that caused the operation to fail
    /// </summary>
    public PlaybackState Playback { get; }

    private static string CreateMessage(PlaybackState playback)
    {
        ArgumentNullException.ThrowIfNull(playback);
        if (!string.IsNullOrWhiteSpace(playback.Reason))
        {
            return $"Playback is unavailable: {playback.Reason}";
        }

        return playback.Availability == PlaybackAvailability.TemporarilyUnavailable
            ? "Playback is temporarily unavailable."
            : "Playback is unavailable.";
    }
}

/// <summary>
/// Indicates that the website requested captcha verification
/// </summary>
public sealed class CaptchaException : ApiException
{
    /// <summary>
    /// Creates an exception with the standard captcha message
    /// </summary>
    public CaptchaException() : base("The website requested captcha verification.") { }
}

/// <summary>
/// Indicates that the website returned an unsuccessful HTTP status
/// </summary>
public sealed class HttpException : ApiException
{
    /// <summary>
    /// Creates an HTTP failure containing the response status and optional reason
    /// </summary>
    /// <param name="statusCode">
    /// HTTP status returned by the website
    /// </param>
    /// <param name="reasonPhrase">
    /// Optional reason phrase returned with the status
    /// </param>
    public HttpException(HttpStatusCode statusCode, string? reasonPhrase)
        : base($"{(int)statusCode}: {reasonPhrase}")
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets the unsuccessful HTTP status returned by the website
    /// </summary>
    public HttpStatusCode StatusCode { get; }
}

/// <summary>
/// Indicates that website markup, JSON, cookies, compression, or stream data could not be read
/// </summary>
public sealed class ParseException : ApiException
{
    /// <summary>
    /// Creates a parsing failure with a readable error message
    /// </summary>
    /// <param name="message">
    /// Text describing which response data could not be read
    /// </param>
    public ParseException(string message) : base(message) { }

    /// <summary>
    /// Creates a parsing failure with a readable error message and its original cause
    /// </summary>
    /// <param name="message">
    /// Text describing which response data could not be read
    /// </param>
    /// <param name="innerException">
    /// Exception raised while reading the response data
    /// </param>
    public ParseException(string message, Exception innerException) : base(message, innerException) { }
}
