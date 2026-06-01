using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxyErrorTrackingService(IProxyErrorDecisionService proxyErrorDecisionService)
    : IProxyErrorTrackingService
{
    private readonly IProxyErrorDecisionService _proxyErrorDecisionService =
        proxyErrorDecisionService;

    public ProxyErrorTrackingResult RegisterError(
        ProxyRuntimeRecord proxy,
        string shortMessage,
        string extendedMessage,
        int errorsToInactiveThreshold,
        DateTime now
    )
    {
        ProxyErrorDecision decision = _proxyErrorDecisionService.Decide(
            proxy.Errors.Select(item => item.Short).ToList(),
            shortMessage,
            errorsToInactiveThreshold
        );

        ProxyRuntimeError? existing = proxy.Errors.LastOrDefault(item =>
            item.Short == shortMessage
        );
        if (decision.IsNewMessage)
            proxy.Errors.Add(
                new ProxyRuntimeError
                {
                    Short = shortMessage,
                    Extended = extendedMessage,
                    Date = now,
                }
            );
        else if (existing is not null)
        {
            existing.TotalDuplicates++;
            existing.Date = now;
        }

        bool disabled = false;
        if (proxy.IsActive && decision.ShouldDisable)
        {
            proxy.IsActive = false;
            disabled = true;
        }

        return new ProxyErrorTrackingResult
        {
            IsNewMessage = decision.IsNewMessage,
            WasDisabled = disabled,
        };
    }
}
