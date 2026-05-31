using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxyErrorDecisionService(IProxyRuntimePolicyService runtimePolicyService)
    : IProxyErrorDecisionService
{
    private readonly IProxyRuntimePolicyService _runtimePolicyService = runtimePolicyService;

    public ProxyErrorDecision Decide(
        IReadOnlyCollection<string> existingMessages,
        string incomingMessage,
        int errorsToInactiveThreshold
    )
    {
        bool isNewMessage = !existingMessages.Contains(incomingMessage, StringComparer.Ordinal);
        int distinctCount = existingMessages.Count + (isNewMessage ? 1 : 0);
        bool shouldDisable = _runtimePolicyService.ShouldDisableProxy(
            distinctCount,
            errorsToInactiveThreshold
        );

        return new ProxyErrorDecision { IsNewMessage = isNewMessage, ShouldDisable = shouldDisable };
    }
}
