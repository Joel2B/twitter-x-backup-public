using Backup.Application.Media.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Media;

public interface IMediaDownloadProjectionService
{
    MediaProcessingResult Project(
        IReadOnlyList<MediaInput> posts,
        MediaDownloadProjectionConfig config
    );
}
