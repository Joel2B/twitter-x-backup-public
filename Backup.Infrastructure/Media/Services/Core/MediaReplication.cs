using Backup.Application.Media;
using Backup.Application.Media.Models;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

public class MediaReplication(
    ILogger<MediaReplication> _logger,
    IMediaReplicationPlanningService mediaReplicationPlanningService,
    IMediaDownloadModelMapper mediaDownloadModelMapper
) : IMediaReplication
{
    private readonly ILogger<MediaReplication> _logger = _logger;
    private readonly IMediaReplicationPlanningService _mediaReplicationPlanningService =
        mediaReplicationPlanningService;
    private readonly IMediaDownloadModelMapper _mediaDownloadModelMapper = mediaDownloadModelMapper;

    public async Task Replicate(
        List<Download> downloads,
        IEnumerable<IMediaStorage> data,
        IMediaStorage target,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        IMediaStorage source = data.First();

        if (target == source)
            return;

        List<Backup.Application.Media.Models.MediaReplicationPathObservation> observations = [];

        foreach (Download download in downloads)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (DataDownload dataDownload in download.Data)
            {
                bool existsSource = await source.Exists(dataDownload.Path);
                bool existsTarget = await target.Exists(dataDownload.Path);

                observations.Add(
                    new Backup.Application.Media.Models.MediaReplicationPathObservation
                    {
                        DownloadId = download.Id,
                        Url = dataDownload.Url,
                        Path = dataDownload.Path,
                        ExistsInSource = existsSource,
                        ExistsInTarget = existsTarget,
                    }
                );
            }
        }

        IReadOnlyList<Backup.Application.Media.Models.MediaReplicationCopyAction> copyActions =
            _mediaReplicationPlanningService.SelectCopyActions(observations);
        List<Backup.Application.Media.Models.MediaReplicationCopyAction> copied = [];

        try
        {
            foreach (
                Backup.Application.Media.Models.MediaReplicationCopyAction action in copyActions
            )
            {
                cancellationToken.ThrowIfCancellationRequested();
                using Stream read = await source.Read(action.Path);
                Stream? staged = null;

                try
                {
                    Stream input = read;

                    if (!read.CanSeek)
                    {
                        staged = target.GetTempStream();
                        await read.CopyToAsync(staged, cancellationToken);
                        staged.Position = 0;
                        input = staged;
                    }
                    else
                        read.Position = 0;

                    await target.Save(input, action.Path, cancellationToken);
                }
                finally
                {
                    staged?.Dispose();
                }

                _logger.LogInformation(
                    "target={TargetId} id={Id} url={Url} path={Path}",
                    target.Id,
                    action.DownloadId,
                    action.Url,
                    action.Path
                );

                copied.Add(action);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("media replication cancelled for target {targetId}", target.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "error replicating media to target {targetId}", target.Id);
        }

        IReadOnlyList<Backup.Application.Media.Models.MediaDownload> remaining =
            _mediaReplicationPlanningService.RemoveCopied(
                _mediaDownloadModelMapper.ToApplication(downloads),
                copied
            );

        downloads.Clear();
        downloads.AddRange(_mediaDownloadModelMapper.ToInfrastructure(remaining));
    }
}
