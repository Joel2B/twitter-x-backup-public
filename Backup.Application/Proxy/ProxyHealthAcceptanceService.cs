using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;

namespace Backup.Application.Proxy;

public sealed class ProxyHealthAcceptanceService(
    IProxyHealthProbeService healthProbeService,
    IProxyAcceptedCandidateFactoryService acceptedCandidateFactoryService,
    IProxyKeyPolicyService keyPolicyService,
    IProxyBatchFlushPolicyService batchFlushPolicyService
) : IProxyHealthAcceptanceService
{
    private readonly IProxyHealthProbeService _healthProbeService = healthProbeService;
    private readonly IProxyAcceptedCandidateFactoryService _acceptedCandidateFactoryService =
        acceptedCandidateFactoryService;
    private readonly IProxyKeyPolicyService _keyPolicyService = keyPolicyService;
    private readonly IProxyBatchFlushPolicyService _batchFlushPolicyService = batchFlushPolicyService;

    public async Task<ProxyHealthAcceptanceResult> AcceptAsync(
        IEnumerable<ProxyRuntimeRecord> merged,
        ISet<string> existingKeys,
        int flushEvery,
        IProxyHealthProbePort probePort,
        CancellationToken cancellationToken = default
    )
    {
        List<ProxyHealthAcceptanceItem> acceptedItems = [];
        List<string> probeErrors = [];
        int acceptedCount = 0;

        foreach (ProxyRuntimeRecord record in merged)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ProxyHealthProbeResult probe = await _healthProbeService.Probe(
                record.Candidate,
                probePort
            );

            if (probe.Error is not null)
                probeErrors.Add(probe.Error.Message ?? "Proxy probe error");

            if (!probe.Success)
                continue;

            ProxyAcceptedCandidate accepted = _acceptedCandidateFactoryService.Create(probe.Candidate);
            ProxyRuntimeRecord acceptedRecord = new()
            {
                Candidate = accepted.Candidate,
                Connections =
                [
                    new ProxyRuntimeConnection
                    {
                        TotalUses = accepted.InitialConnectionUses,
                    },
                ],
            };

            string key = _keyPolicyService.Build(
                acceptedRecord.Candidate.Ip,
                acceptedRecord.Candidate.Port,
                acceptedRecord.Candidate.Protocol
            );

            if (!existingKeys.Add(key))
                continue;

            acceptedCount++;

            acceptedItems.Add(
                new ProxyHealthAcceptanceItem
                {
                    Record = acceptedRecord,
                    ShouldFlush = _batchFlushPolicyService.ShouldFlush(acceptedCount, flushEvery),
                }
            );
        }

        return new ProxyHealthAcceptanceResult
        {
            AcceptedItems = acceptedItems,
            ProbeErrors = probeErrors,
        };
    }
}
