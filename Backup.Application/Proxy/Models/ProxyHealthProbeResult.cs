using System.Net;

namespace Backup.Application.Proxy.Models;

public sealed class ProxyHealthProbeResult
{
    public required ProxyCandidate Candidate { get; init; }

    public required HttpStatusCode? StatusCode { get; init; }

    public required bool Success { get; init; }

    public Exception? Error { get; init; }
}
