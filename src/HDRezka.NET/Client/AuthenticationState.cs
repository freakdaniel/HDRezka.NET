namespace HdRezka;

/// <summary>
/// Describes authentication verified against a protected website page
/// </summary>
/// <param name="IsAuthenticated">
/// <see langword="true"/> when the protected page was available without showing the login form
/// </param>
/// <param name="VerificationUri">
/// Protected page used to verify the current cookies
/// </param>
/// <param name="CookieNames">
/// Names of cookies stored for the website without exposing their values
/// </param>
public sealed record AuthenticationState(
    bool IsAuthenticated,
    Uri VerificationUri,
    IReadOnlyCollection<string> CookieNames)
{
    /// <summary>
    /// Gets the subscription tier detected from the authenticated page
    /// </summary>
    public AccountTier AccountTier { get; init; }

    /// <summary>
    /// Gets whether the current session belongs to an account with an active Premium subscription
    /// </summary>
    public bool IsPremium => AccountTier == AccountTier.Premium;
}
