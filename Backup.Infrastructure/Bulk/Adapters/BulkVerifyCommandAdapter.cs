using Backup.Application.Bulk.Models;
using Backup.Application.Bulk.Ports;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Bulk.Models;
using Backup.Infrastructure.Posts.Abstractions.Data;

namespace Backup.Infrastructure.Bulk.Adapters;

internal sealed class BulkVerifyCommandAdapter(IPostDomainData postData, IBulkData bulkData)
    : IBulkVerifyCommand
{
    private readonly IPostDomainData _postData = postData;
    private readonly IBulkData _bulkData = bulkData;

    public async Task<IReadOnlyList<BulkItem>> GetBulks()
    {
        List<BulkData> bulks = await _bulkData.GetBulks() ?? [];
        return bulks.Select(BulkPhaseItemMapper.ToApplication).ToList();
    }

    public Task<Dictionary<string, int>> GetPostCountsByProfileIds(
        IReadOnlyCollection<string> profileIds
    ) => _postData.GetPostCountsByProfileIds(profileIds);
}
