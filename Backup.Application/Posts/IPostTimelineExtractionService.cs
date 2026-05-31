using Newtonsoft.Json.Linq;

namespace Backup.Application.Posts;

public interface IPostTimelineExtractionService
{
    IReadOnlyList<JObject> ExtractEntries(JObject root);
    string? ExtractCursor(JObject root);
}
