namespace HdRezka.Abstractions;

internal sealed record AuthenticationPageSnapshot(
    bool IsLoginPage,
    AccountTier AccountTier);
