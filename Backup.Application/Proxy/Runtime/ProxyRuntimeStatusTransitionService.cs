using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxyRuntimeStatusTransitionService : IProxyRuntimeStatusTransitionService
{
    public bool ShouldHandleError(bool isCurrentlyActive) => isCurrentlyActive;

    public DateTime? ResolveDisabledAt(bool wasDisabled, DateTime now) => wasDisabled ? now : null;

    public ProxyRuntimeStatusTransitionResult ResolveStatus(
        bool runtimeIsActive,
        DateTime? previousStatusDate,
        DateTime? disabledAt
    ) =>
        runtimeIsActive
            ? new ProxyRuntimeStatusTransitionResult
            {
                IsActive = true,
                StatusDate = previousStatusDate,
            }
            : new ProxyRuntimeStatusTransitionResult
            {
                IsActive = false,
                StatusDate = disabledAt ?? previousStatusDate,
            };
}
