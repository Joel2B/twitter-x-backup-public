using Backup.Application.Posts;
using Backup.Infrastructure.Posts.Models;
using Newtonsoft.Json.Linq;

namespace Backup.Infrastructure.Posts.Adapters.Parsing;

internal static class TimelineEntryExtractor
{
    private static readonly IPostTimelineExtractionService Service = new PostTimelineExtractionService();

    public static List<Entry> ExtractEntries(JObject root)
    {
        IReadOnlyList<JObject> entries = Service.ExtractEntries(root);
        return entries.Select(token => token.ToObject<Entry>() ?? throw new Exception()).ToList();
    }

    public static string? ExtractCursor(JObject root) => Service.ExtractCursor(root);
}
