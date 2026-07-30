namespace HdRezka;

/// <summary>
/// Identifies the subscription tier detected for the current website session
/// </summary>
public enum AccountTier
{
    /// <summary>
    /// The page did not provide enough information to determine the subscription tier
    /// </summary>
    Unknown,

    /// <summary>
    /// The authenticated account does not have an active Premium subscription
    /// </summary>
    Standard,

    /// <summary>
    /// The authenticated account has an active Premium subscription
    /// </summary>
    Premium
}
