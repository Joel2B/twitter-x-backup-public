using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Posts.Models;

namespace Backup.Infrastructure.Core.Media;

public class MediaProcessorContext(
    List<MediaInput> posts,
    Dictionary<string, Download> all,
    Dictionary<string, Download> filtered
)
{
    public IReadOnlyList<MediaInput> Posts = posts;

    public Dictionary<string, Download> All = all;
    public Dictionary<string, Download> Filtered = filtered;
}
