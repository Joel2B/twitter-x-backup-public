namespace Backup.Application.Proxy;

public sealed class ProxyConnectionWindowPolicyService : IProxyConnectionWindowPolicyService
{
    private const string Format = "yyyy-MM-dd, HH";

    public string GetWindowKey(DateTime value) => value.ToString(Format);

    public bool IsSameWindow(DateTime left, DateTime right) =>
        string.Equals(GetWindowKey(left), GetWindowKey(right), StringComparison.Ordinal);
}
