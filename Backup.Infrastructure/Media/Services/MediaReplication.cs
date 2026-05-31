using Backup.Infrastructure.Logging;
using Backup.Application.Media;
using Backup.Application.Media.Models;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

public class MediaReplication(
    ILogger<MediaReplication> _logger,
    IMediaReplicationPlanningService mediaReplicationPlanningService
) : IMediaReplication
{
    private readonly ILogger<MediaReplication> _logger = _logger;
    private readonly IMediaReplicationPlanningService _mediaReplicationPlanningService =
        mediaReplicationPlanningService;

    public async Task Replicate(
        List<Download> downloads,
        IEnumerable<IMediaStorage> data,
        IMediaStorage target
    )
    {
        IMediaStorage source = data.First();

        if (target == source)
            return;

        List<MediaReplicationPathObservation> observations = [];

        foreach (Download download in downloads)
        {
            foreach (DataDownload dataDownload in download.Data)
            {
                bool existsSource = await source.Exists(dataDownload.Path);
                bool existsTarget = await target.Exists(dataDownload.Path);

                observations.Add(
                    new MediaReplicationPathObservation
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

        IReadOnlyList<MediaReplicationCopyAction> copyActions =
            _mediaReplicationPlanningService.SelectCopyActions(observations);
        List<MediaReplicationCopyAction> copied = [];

        try
        {
            foreach (MediaReplicationCopyAction action in copyActions)
            {
                using Stream read = await source.Read(action.Path);
                using Stream write = await target.Write(action.Path);

                await read.CopyToAsync(write);

                _logger.LogInformation(
                    target.Id,
                    "{Id}, {Url}, {Path}",
                    action.DownloadId,
                    action.Url,
                    action.Path
                );

                copied.Add(action);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {error}", ex.Message);
        }

        IReadOnlyList<Backup.Application.Media.Models.MediaDownload> remaining =
            _mediaReplicationPlanningService.RemoveCopied(ToApplication(downloads), copied);

        SyncDownloads(downloads, remaining);
    }

    private static List<Backup.Application.Media.Models.MediaDownload> ToApplication(
        IEnumerable<Download> downloads
    ) =>
        downloads
            .Select(download => new Backup.Application.Media.Models.MediaDownload
            {
                Id = download.Id,
                Data = download
                    .Data.Select(item => new MediaDownloadData { Url = item.Url, Path = item.Path })
                    .ToList(),
            })
            .ToList();

    private static void SyncDownloads(
        List<Download> target,
        IReadOnlyList<Backup.Application.Media.Models.MediaDownload> source
    )
    {
        target.Clear();
        target.AddRange(
            source.Select(download => new Download
            {
                Id = download.Id,
                Data = download
                    .Data.Select(item => new DataDownload { Url = item.Url, Path = item.Path })
                    .ToList(),
            })
        );
    }
}
