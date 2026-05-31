namespace Backup.Application.Proxy.Models;

public sealed class ProxyRuntimeRecord
{
    public required ProxyCandidate Candidate { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ProxyRuntimeConnection> Connections { get; set; } = [];
    public List<ProxyRuntimeError> Errors { get; set; } = [];
}
