using Newtonsoft.Json.Linq;

namespace Backup.Infrastructure.Posts.Abstractions.Services;

public interface IPostTimelineExtractionService
{
    IReadOnlyList<JObject> ExtractEntries(JObject root);
    string? ExtractCursor(JObject root);
}
