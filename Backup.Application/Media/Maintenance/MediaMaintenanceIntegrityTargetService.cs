using Backup.Application.Media.Maintenance.Models;
using Backup.Application.Media.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaMaintenanceIntegrityTargetService : IMediaMaintenanceIntegrityTargetService
{
    public IReadOnlyList<MediaMaintenanceIntegrityTarget> BuildTargets(
        IReadOnlyList<MediaDownload> downloads
    )
    {
        List<MediaMaintenanceIntegrityTarget> targets = [];

        for (int d = 0; d < downloads.Count; d++)
        {
            MediaDownload download = downloads[d];

            for (int i = 0; i < download.Data.Count; i++)
            {
                targets.Add(
                    new MediaMaintenanceIntegrityTarget
                    {
                        CorrelationId = $"{d}:{i}",
                        Path = download.Data[i].Path,
                    }
                );
            }
        }

        return targets;
    }

    public IReadOnlyList<MediaDownload> RemoveByCorrelations(
        IReadOnlyList<MediaDownload> downloads,
        IReadOnlySet<string> removeCorrelations
    )
    {
        if (removeCorrelations.Count == 0)
            return downloads.Select(Clone).ToList();

        List<MediaDownload> result = [];

        for (int d = 0; d < downloads.Count; d++)
        {
            MediaDownload source = downloads[d];
            MediaDownload target = new() { Id = source.Id, Data = [] };

            for (int i = 0; i < source.Data.Count; i++)
            {
                if (removeCorrelations.Contains($"{d}:{i}"))
                    continue;

                target.Data.Add(
                    new MediaDownloadData { Url = source.Data[i].Url, Path = source.Data[i].Path }
                );
            }

            if (target.Data.Count > 0)
                result.Add(target);
        }

        return result;
    }

    private static MediaDownload Clone(MediaDownload source) =>
        new()
        {
            Id = source.Id,
            Data = source
                .Data.Select(item => new MediaDownloadData { Url = item.Url, Path = item.Path })
                .ToList(),
        };
}
