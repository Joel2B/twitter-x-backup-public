namespace Backup.Application.Proxy;

public sealed class ProxyHealthCheckPolicyService : IProxyHealthCheckPolicyService
{
    private readonly string _url;
    private readonly TimeSpan _timeout;

    public ProxyHealthCheckPolicyService(string url, int timeoutSeconds)
    {
        if (
            !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        )
            throw new ArgumentException("Proxy health check URL must be absolute HTTP or HTTPS.", nameof(url));

        if (timeoutSeconds <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(timeoutSeconds),
                "Proxy health check timeout must be positive."
            );

        _url = url;
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    public string GetHealthCheckUrl() => _url;

    public TimeSpan GetHealthCheckTimeout() => _timeout;

    public bool ShouldFallbackToHttp(Exception exception) =>
        exception.Message.Contains(
            "The SSL connection could not be established",
            StringComparison.Ordinal
        );
}
