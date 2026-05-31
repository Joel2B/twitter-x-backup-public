using Backup.Application.Bulk.Ports;
using Backup.Application.Bulk.Models;
using Backup.Application.Bulk;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Bulk.Abstractions.Services;
using Backup.Infrastructure.Bulk.Models;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Domain.Posts;

namespace Backup.Infrastructure.Bulk.Adapters;

internal sealed class BulkPhase1CommandAdapter(
    IReadOnlyDictionary<string, ApiConfig> api,
    IBulkItemIdentityService bulkItemIdentityService,
    IBulkIdentityLastWriteWinsService bulkIdentityLastWriteWinsService,
    IPostDomainData postData,
    IBulkData bulkData,
    IBulkApiClient bulkApiClient
) : IBulkPhase1Command
{
    private readonly IReadOnlyDictionary<string, ApiConfig> _api = api;
    private readonly IBulkItemIdentityService _bulkItemIdentityService = bulkItemIdentityService;
    private readonly IBulkIdentityLastWriteWinsService _bulkIdentityLastWriteWinsService =
        bulkIdentityLastWriteWinsService;
    private readonly IPostDomainData _postData = postData;
    private readonly IBulkData _bulkData = bulkData;
    private readonly IBulkApiClient _bulkApiClient = bulkApiClient;

    private List<BulkData>? _sourceBulks;
    private Dictionary<string, BulkData>? _bulkMap;

    public Task<int> GetPostCount() => _postData.GetCount();

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

    public Task<ParseResult?> GetUserMedia(
        string userId,
        string origin,
        int count,
        string? cursor,
        CancellationToken cancellationToken
    ) => _bulkApiClient.GetUserMedia(_api, userId, origin, count, cursor, cancellationToken);

    public Task AddPosts(string userId, string origin, List<Post> posts) =>
        _postData.AddPosts(userId, origin, posts);

    public Task SavePosts() => _postData.Save();
}
