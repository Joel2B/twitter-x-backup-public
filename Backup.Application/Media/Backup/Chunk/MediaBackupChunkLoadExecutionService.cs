using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkLoadExecutionService(
    IMediaBackupChunkLoadDecisionService mediaBackupChunkLoadDecisionService,
    IMediaBackupChunkReadFailurePolicyService mediaBackupChunkReadFailurePolicyService
) : IMediaBackupChunkLoadExecutionService
{
    private readonly IMediaBackupChunkLoadDecisionService _mediaBackupChunkLoadDecisionService =
        mediaBackupChunkLoadDecisionService;
    private readonly IMediaBackupChunkReadFailurePolicyService _mediaBackupChunkReadFailurePolicyService =
        mediaBackupChunkReadFailurePolicyService;

    public async Task<IReadOnlyList<TChunk>?> ExecuteAsync<TChunk>(
        string? chunkDataFileName,
        IReadOnlyCollection<int>? chunkIds,
        CancellationToken token,
        Func<MediaBackupChunkReadDescriptor, CancellationToken, Task<TChunk>> readChunk,
        Action<int>? onChunkProcessed = null
    )
    {
        MediaBackupChunkLoadDecision decision = _mediaBackupChunkLoadDecisionService.Decide(
            chunkDataFileName,
            chunkIds?.ToList()
        );

        if (decision.Action == MediaBackupChunkLoadAction.SkipAsNull)
            return null;

        if (decision.Action == MediaBackupChunkLoadAction.ReturnEmpty)
            return [];

        TChunk[] chunks = new TChunk[decision.ReadDescriptors.Count];
        ParallelOptions options = new() { MaxDegreeOfParallelism = 16, CancellationToken = token };

        try
        {
            await Parallel.ForEachAsync(
                decision.ReadDescriptors,
                options,
                async (descriptor, ct) =>
                {
                    TChunk chunk = await readChunk(descriptor, ct);
                    chunks[descriptor.Index] = chunk;
                    onChunkProcessed?.Invoke(descriptor.Index);
                }
            );
        }
        catch (Exception ex)
        {
            MediaBackupChunkReadFailureAction action =
                _mediaBackupChunkReadFailurePolicyService.Decide(ex, token.IsCancellationRequested);

            if (action == MediaBackupChunkReadFailureAction.Throw)
                throw;

            return null;
        }

        return chunks;
    }
}
