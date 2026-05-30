namespace Backup.Application.Proxy;

public interface IProxyHealthCheckPolicyService
{
    string GetHealthCheckUrl();
    TimeSpan GetHealthCheckTimeout();
    bool ShouldFallbackToHttp(Exception exception);
}
