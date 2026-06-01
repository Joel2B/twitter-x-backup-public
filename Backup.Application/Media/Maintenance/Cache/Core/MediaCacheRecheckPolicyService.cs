namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheRecheckPolicyService : IMediaCacheRecheckPolicyService
{
    public bool ShouldRecheck(long? streamSizeBytes, long? fileSizeBytes) =>
        streamSizeBytes is not null && (fileSizeBytes is null || fileSizeBytes == 0);
}
