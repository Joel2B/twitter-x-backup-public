using Backup.Application.Bulk.Ports;
using Backup.Application.Bulk.Models;
using Backup.Application.Bulk;
using Backup.Domain.Posts;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Bulk.Abstractions.Services;
using Backup.Infrastructure.Bulk.Models;
using Backup.Infrastructure.Models.Config.Api;

namespace Backup.Infrastructure.Bulk.Adapters;

internal sealed class BulkImportCommandAdapter(
    IReadOnlyDictionary<string, ApiConfig> api,
    IBulkItemIdentityService bulkItemIdentityService,
    IBulkIdentityLastWriteWinsService bulkIdentityLastWriteWinsService,
    IBulkSourceData bulkSourceData,
    IBulkData bulkData,
    IBulkApiClient bulkApiClient
) : IBulkImportCommand
{
    private readonly IReadOnlyDictionary<string, ApiConfig> _api = api;
    private readonly IBulkItemIdentityService _bulkItemIdentityService = bulkItemIdentityService;
    private readonly IBulkIdentityLastWriteWinsService _bulkIdentityLastWriteWinsService =
        bulkIdentityLastWriteWinsService;
    private readonly IBulkSourceData _bulkSourceData = bulkSourceData;
    private readonly IBulkData _bulkData = bulkData;
    private readonly IBulkApiClient _bulkApiClient = bulkApiClient;

    private List<BulkData>? _sourceBulks;
    private Dictionary<string, BulkData>? _bulkMap;

    public async Task<IReadOnlyList<BulkSourceItem>> GetSources()
    {
        List<Source> sources = await _bulkSourceData.GetSources();
        return sources.Select(BulkSourceItemMapper.ToApplication).ToList();
    }

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

    public Task<bool> VerifyApi() => _bulkApiClient.Verify();

    public Task<ParseUser?> GetUserByUser(string userName, CancellationToken cancellationToken) =>
        _bulkApiClient.GetUserByUser(_api, userName, cancellationToken);
}
