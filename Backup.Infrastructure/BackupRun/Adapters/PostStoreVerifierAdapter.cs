using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Posts.Data;
using Backup.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.BackupRun.Adapters;

public class PostStoreVerifierAdapter(
    PostDataMultiStore postData,
    ILogger<PostStoreVerifierAdapter> logger
) : IPostStoreVerifier
{
    private readonly PostDataMultiStore _postData = postData;
    private readonly ILogger<PostStoreVerifierAdapter> _logger = logger;

    public async Task Verify()
    {
        using (_logger.LogTimer("post store parity check"))
            await _postData.VerifyStoreCounts();
    }
}
