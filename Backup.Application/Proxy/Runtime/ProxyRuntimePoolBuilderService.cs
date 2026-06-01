using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxyRuntimePoolBuilderService(
    IProxyRuntimeRecordMergeService runtimeRecordMergeService,
    IProxyRuntimePoolProjectionService runtimePoolProjectionService
) : IProxyRuntimePoolBuilderService
{
    private readonly IProxyRuntimeRecordMergeService _runtimeRecordMergeService =
        runtimeRecordMergeService;
    private readonly IProxyRuntimePoolProjectionService _runtimePoolProjectionService =
        runtimePoolProjectionService;

    public IReadOnlyList<ProxyRuntimeRecord> BuildPool(
        IEnumerable<ProxyRuntimeRecord> stored,
        IEnumerable<ProxyCandidate> loaded
    )
    {
        IReadOnlyList<ProxyRuntimeRecord> merged = _runtimeRecordMergeService.MergeStoredAndLoaded(
            stored,
            loaded
        );
        return _runtimePoolProjectionService.SelectPool(merged);
    }
}
