using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxyErrorDecisionService
{
    ProxyErrorDecision Decide(
        IReadOnlyCollection<string> existingMessages,
        string incomingMessage,
        int errorsToInactiveThreshold
    );
}
