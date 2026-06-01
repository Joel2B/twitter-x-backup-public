using Backup.Application.Bulk;
using Backup.Application.Bulk.Models;
using Backup.Application.Bulk.Ports;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Bulk.Models;

namespace Backup.Infrastructure.Bulk.Adapters;

internal sealed class BulkPhase2ResetCommandAdapter(
    IBulkData bulkData,
    IBulkItemIdentityService bulkItemIdentityService,
    IBulkIdentityLastWriteWinsService bulkIdentityLastWriteWinsService
) : IBulkPhase2ResetCommand
{
    private readonly IBulkData _bulkData = bulkData;
    private readonly IBulkItemIdentityService _bulkItemIdentityService = bulkItemIdentityService;
    private readonly IBulkIdentityLastWriteWinsService _bulkIdentityLastWriteWinsService =
        bulkIdentityLastWriteWinsService;

    private List<BulkData>? _sourceBulks;
    private Dictionary<string, BulkData>? _bulkMap;

    public async Task<IReadOnlyList<BulkItem>> GetBulks()
    {
        _sourceBulks = await _bulkData.GetBulks() ?? [];

        List<BulkItem> items = _sourceBulks.Select(BulkPhaseItemMapper.ToApplication).ToList();
        IReadOnlyDictionary<string, int> lastSourceIndexByKey =
            _bulkIdentityLastWriteWinsService.BuildLastIndexByKey(
                items.Select(_bulkItemIdentityService.GetKey)
            );
        _bulkMap = lastSourceIndexByKey.ToDictionary(
            entry => entry.Key,
            entry => _sourceBulks[entry.Value]
        );

        return items;
    }

    public async Task SaveBulks(IReadOnlyList<BulkItem> bulks)
    {
        if (_sourceBulks is null || _bulkMap is null)
            return;

        foreach (BulkItem bulk in bulks)
        {
            if (!_bulkMap.TryGetValue(_bulkItemIdentityService.GetKey(bulk), out BulkData? source))
                continue;

            BulkPhaseItemMapper.ApplyToInfrastructure(bulk, source);
        }

        await _bulkData.Save(_sourceBulks);
    }
}
