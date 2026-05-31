namespace Backup.Application.Proxy;

public sealed class ProxyKeyPolicyService : IProxyKeyPolicyService
{
    public string Build(string ip, string port, string protocol) =>
        $"{ip}:{port}:{protocol}".ToLowerInvariant();
}
