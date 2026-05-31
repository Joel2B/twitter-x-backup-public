using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxyUsageTrackingService(
    IProxyConnectionWindowPolicyService connectionWindowPolicyService
) : IProxyUsageTrackingService
{
    private readonly IProxyConnectionWindowPolicyService _connectionWindowPolicyService =
        connectionWindowPolicyService;

    public void RegisterUse(ProxyRuntimeRecord proxy, DateTime now)
    {
        ProxyRuntimeConnection? connection = proxy.Connections.LastOrDefault(item =>
            _connectionWindowPolicyService.IsSameWindow(item.Date, now)
        );

        if (connection is null)
        {
            connection = new ProxyRuntimeConnection { Date = now };
            proxy.Connections.Add(connection);
        }

        connection.TotalUses++;
    }
}
