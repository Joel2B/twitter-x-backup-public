using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Interfaces.Services.Media;
using Backup.Infrastructure.Models.Media;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Services.Media;

public class MediaReplication(ILogger<MediaReplication> _logger) : IMediaReplication
{
    private readonly ILogger<MediaReplication> _logger = _logger;

    public async Task Replicate(
        List<Download> downloads,
        IEnumerable<IMediaStorage> data,
        IMediaStorage target
    )
    {
        IMediaStorage source = data.First();

        if (target == source)
            return;

        List<DataDownload> copied = [];

        try
        {
            foreach (Download download in downloads)
            {
                foreach (DataDownload dataDownload in download.Data)
                {
                    bool existsSource = await source.Exists(dataDownload.Path);
                    bool existsTarget = await target.Exists(dataDownload.Path);

                    if (!existsSource || existsTarget)
                        continue;

                    using Stream read = await source.Read(dataDownload.Path);
                    using Stream write = await target.Write(dataDownload.Path);

                    await read.CopyToAsync(write);

                    _logger.LogInformation(
                        target.Id,
                        "{Id}, {Url}, {Path}",
                        download.Id,
                        dataDownload.Url,
                        dataDownload.Path
                    );

                    copied.Add(dataDownload);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {error}", ex.Message);
        }

        foreach (Download download in downloads)
            download.Data.RemoveAll(copied.Contains);

        downloads.RemoveAll(dl => dl.Data.Count == 0);
    }
}

