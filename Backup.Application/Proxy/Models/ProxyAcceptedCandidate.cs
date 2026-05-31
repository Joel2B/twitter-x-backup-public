namespace Backup.Application.Proxy.Models;

public sealed class ProxyAcceptedCandidate
{
    public required ProxyCandidate Candidate { get; init; }

    public required int InitialConnectionUses { get; init; }
}
