namespace HdRezka;

/// <summary>
/// Creates persistent authentication cookies accepted by DLE-based mirrors
/// </summary>
public static class AuthenticationCookies
{
    /// <summary>
    /// Creates the cookie values required to restore an existing authenticated account
    /// </summary>
    /// <param name="userId">
    /// Value of the website cookie named <c>dle_user_id</c>
    /// </param>
    /// <param name="passwordHash">
    /// Value of the website cookie named <c>dle_password</c>
    /// </param>
    /// <returns>
    /// New dictionary containing <c>dle_user_id</c> and <c>dle_password</c>
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="userId"/> or <paramref name="passwordHash"/> is empty or contains only whitespace
    /// </exception>
    public static IReadOnlyDictionary<string, string> Create(
        string userId,
        string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        return new Dictionary<string, string>
        {
            ["dle_user_id"] = userId,
            ["dle_password"] = passwordHash
        };
    }
}
