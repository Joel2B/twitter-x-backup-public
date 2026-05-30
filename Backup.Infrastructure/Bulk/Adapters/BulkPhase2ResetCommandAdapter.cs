using Backup.Application.Bulk.Ports;
using Backup.Application.Bulk.Models;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Bulk.Models;

namespace Backup.Infrastructure.Bulk.Adapters;

internal sealed class BulkPhase2ResetCommandAdapter(IBulkData bulkData) : IBulkPhase2ResetCommand
{
    private readonly IBulkData _bulkData = bulkData;

    private List<BulkData>? _sourceBulks;
    private Dictionary<BulkItem, BulkData>? _bulkMap;

    public async Task<IReadOnlyList<BulkItem>> GetBulks()
    {
        _sourceBulks = await _bulkData.GetBulks() ?? [];

        List<BulkItem> items = _sourceBulks.Select(BulkPhaseItemMapper.ToApplication).ToList();
        _bulkMap = items.Zip(_sourceBulks).ToDictionary(pair => pair.First, pair => pair.Second);

        return items;
    }

    public async Task SaveBulks(IReadOnlyList<BulkItem> bulks)
    {
        if (_sourceBulks is null || _bulkMap is null)
            return;

        foreach (BulkItem bulk in bulks)
        {
            if (!_bulkMap.TryGetValue(bulk, out BulkData? source))
                continue;

            BulkPhaseItemMapper.ApplyToInfrastructure(bulk, source);
        }

        await _bulkData.Save(_sourceBulks);
    }
}
