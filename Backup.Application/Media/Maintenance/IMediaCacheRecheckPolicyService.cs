namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheRecheckPolicyService
{
    bool ShouldRecheck(long? streamSizeBytes, long? fileSizeBytes);
}
