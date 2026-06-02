using Backup.Application.Proxy.Models;
using Backup.Infrastructure.Proxy.Models;

namespace Backup.Infrastructure.Proxy.Abstractions.Core;

public interface IProxyRuntimeMutationService
{
    ProxyUseHandlingOutcome HandleUse(ProxyData proxy, int stopCount);
    ProxyErrorHandlingOutcome HandleError(ProxyData proxy, Exception exception, int errorsToInactive);
}
