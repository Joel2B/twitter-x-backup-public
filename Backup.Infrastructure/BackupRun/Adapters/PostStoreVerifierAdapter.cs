using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.BackupRun.Adapters;

public class PostStoreVerifierAdapter(
    IPostStoreParityVerifier postStoreParityVerifier,
    ILogger<PostStoreVerifierAdapter> logger
) : IPostStoreVerifier
{
    private readonly IPostStoreParityVerifier _postStoreParityVerifier = postStoreParityVerifier;
    private readonly ILogger<PostStoreVerifierAdapter> _logger = logger;

    public async Task Verify()
    {
        using (_logger.LogTimer("post store parity check"))
            await _postStoreParityVerifier.VerifyStoreCounts();
    }
}
