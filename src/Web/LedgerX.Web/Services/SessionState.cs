using LedgerX.Contracts.Api;

namespace LedgerX.Web.Services;

public sealed class SessionState
{
    public TokenResponse? Session { get; private set; }

    public bool IsAuthenticated => Session is not null;

    public bool IsStaff => Session?.Roles.Any(r => r is "Administrator" or "Auditor" or "FinanceOperator" or "SupportAgent") == true;

    public bool IsAdmin => Session?.Roles.Contains("Administrator") == true;

    public bool IsAuditor => Session?.Roles.Contains("Auditor") == true;

    public string DisplayName => Session?.DisplayName ?? "Guest";

    public string? AccessToken => Session?.AccessToken;

    public event Action? Changed;

    public void SignIn(TokenResponse token)
    {
        Session = token;
        Changed?.Invoke();
    }

    public void SignOut()
    {
        Session = null;
        Changed?.Invoke();
    }
}
