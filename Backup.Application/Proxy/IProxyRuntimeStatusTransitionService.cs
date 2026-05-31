using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxyRuntimeStatusTransitionService
{
    bool ShouldHandleError(bool isCurrentlyActive);
    DateTime? ResolveDisabledAt(bool wasDisabled, DateTime now);
    ProxyRuntimeStatusTransitionResult ResolveStatus(
        bool runtimeIsActive,
        DateTime? previousStatusDate,
        DateTime? disabledAt
    );
}
