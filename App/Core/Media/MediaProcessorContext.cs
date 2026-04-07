using Backup.App.Models.Media;
using Backup.App.Models.Post;

namespace Backup.App.Core.Media;

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
