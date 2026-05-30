namespace Backup.Application.Proxy;

public interface IProxyConnectionWindowPolicyService
{
    string GetWindowKey(DateTime value);
    bool IsSameWindow(DateTime left, DateTime right);
}
