namespace Backup.Application.Proxy;

public sealed class ProxyHealthCheckPolicyService : IProxyHealthCheckPolicyService
{
    public string GetHealthCheckUrl() =>
        "https://pbs.twimg.com/media/G6hPY2KbIAAm-FB?format=jpg&name=large";

    public TimeSpan GetHealthCheckTimeout() => TimeSpan.FromSeconds(10);

    public bool ShouldFallbackToHttp(Exception exception) =>
        exception.Message.Contains(
            "The SSL connection could not be established",
            StringComparison.Ordinal
        );
}
