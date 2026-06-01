namespace Backup.Application.Media.Backup;

public sealed class MediaBackupDirectApplyPathService(
    IMediaBackupDirectPathQueueService directPathQueueService
) : IMediaBackupDirectApplyPathService
{
    private readonly IMediaBackupDirectPathQueueService _directPathQueueService =
        directPathQueueService;

    public IReadOnlyList<string> GetPaths(IEnumerable<string> directPaths) =>
        _directPathQueueService.Normalize(directPaths);
}
