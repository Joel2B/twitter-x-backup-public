using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Data.Posts;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.BackupRun.Adapters;

public class PostStoreVerifierAdapter(
    IPostData postData,
    ILogger<PostStoreVerifierAdapter> logger
) : IPostStoreVerifier
{
    private readonly IPostData _postData = postData;
    private readonly ILogger<PostStoreVerifierAdapter> _logger = logger;

    public async Task Verify()
    {
        if (_postData is not PostDataMultiStore stores)
            return;

        using (_logger.LogTimer("post store parity check"))
            await stores.VerifyStoreCounts();
    }
}
