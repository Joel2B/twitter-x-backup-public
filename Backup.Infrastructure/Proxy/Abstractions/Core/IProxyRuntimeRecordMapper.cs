using Backup.Application.Proxy.Models;
using Backup.Infrastructure.Proxy.Models;

namespace Backup.Infrastructure.Proxy.Abstractions.Core;

public interface IProxyRuntimeRecordMapper
{
    ProxyRuntimeRecord ToRuntimeRecord(ProxyData data);
    ProxyData ToProxyData(ProxyRuntimeRecord record);
    void ApplyRuntimeRecord(ProxyData proxy, ProxyRuntimeRecord source, DateTime? disabledAt);
}
