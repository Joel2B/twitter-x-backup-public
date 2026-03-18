using Backup.App.Interfaces.Services.Media;
using Backup.App.Models.Media;
using Backup.App.Models.Post;

namespace Backup.App.Core.Media;

public class MediaProcessorContext(
    List<Post> posts,
    Dictionary<string, Download> all,
    Dictionary<string, Download> filtered,
    IMediaProcessingLogger logger
)
{
    public IReadOnlyList<Post> Posts = posts;

    public Dictionary<string, Download> All = all;
    public Dictionary<string, Download> Filtered = filtered;
    public readonly IMediaProcessingLogger logger = logger;
}
