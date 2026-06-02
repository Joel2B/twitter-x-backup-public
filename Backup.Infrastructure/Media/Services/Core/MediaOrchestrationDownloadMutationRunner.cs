using Backup.Application.Media.Models;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaOrchestrationDownloadMutationRunner(
    IMediaDownloadModelMapper mediaDownloadModelMapper
)
{
    private readonly IMediaDownloadModelMapper _mediaDownloadModelMapper = mediaDownloadModelMapper;

    public async Task Execute(
        List<MediaDownload> downloads,
        Func<List<Download>, CancellationToken, Task> action,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<Download> infrastructureDownloads = _mediaDownloadModelMapper.ToInfrastructure(
            downloads
        );
        await action(infrastructureDownloads, cancellationToken);

        downloads.Clear();
        downloads.AddRange(_mediaDownloadModelMapper.ToApplication(infrastructureDownloads));
    }
}
