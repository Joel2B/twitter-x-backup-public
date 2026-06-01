using Backup.Application.Posts;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Posts.Adapters;

public class PostReplicationAdapter(
    IPostReplicationService postReplicationService,
    ILogger<PostReplicationAdapter> logger
) : IPostReplication
{
    private readonly IPostReplicationService _postReplicationService = postReplicationService;
    private readonly ILogger<PostReplicationAdapter> _logger = logger;

    public async Task Replicate(IEnumerable<IPostDataStore> stores)
    {
        try
        {
            List<PostReplicationStoreAdapter> adapters = stores
                .Select(store => new PostReplicationStoreAdapter(
                    store as IPostDomainDataStore ?? new PostDataDomainStoreAdapter(store)
                ))
                .ToList();

            await _postReplicationService.Replicate(adapters);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {error}", JsonConvert.SerializeObject(ex));
        }
    }
}
