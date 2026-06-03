using Backup.Application.Media.Models;

namespace Backup.Application.Media;

public interface IMediaDownloadDataBuilderService
{
    MediaDownloadData Build(MediaDownloadDataBuildInput input);
}
