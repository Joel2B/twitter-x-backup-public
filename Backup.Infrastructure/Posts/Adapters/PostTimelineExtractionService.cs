using Backup.Infrastructure.Posts.Abstractions.Services;
using Newtonsoft.Json.Linq;

namespace Backup.Infrastructure.Posts.Adapters;

public sealed class PostTimelineExtractionService : IPostTimelineExtractionService
{
    public IReadOnlyList<JObject> ExtractEntries(JObject root)
    {
        IReadOnlyList<JObject>? entries = GetTimelineItems(root);

        if (entries is null || entries.Count == 0)
            entries = GetTimelineModuleItems(root);

        if (entries is null || entries.Count == 0)
            entries = GetModuleItems(root);

        return entries ?? [];
    }

    public string? ExtractCursor(JObject root)
    {
        JArray entries = GetEntriesArray(root);

        IEnumerable<JObject> cursorEntries = entries
            .OfType<JObject>()
            .Where(entry =>
                string.Equals(
                    entry.SelectToken("content.entryType")?.Value<string>(),
                    "TimelineTimelineCursor",
                    StringComparison.Ordinal
                )
            );

        return cursorEntries
            .FirstOrDefault(entry =>
                string.Equals(
                    entry.SelectToken("content.cursorType")?.Value<string>(),
                    "Bottom",
                    StringComparison.Ordinal
                )
            )
            ?.SelectToken("content.value")
            ?.Value<string>();
    }

    private static IReadOnlyList<JObject>? GetTimelineItems(JObject root)
    {
        JArray entries = GetEntriesArray(root);

        List<JObject> items = entries
            .OfType<JObject>()
            .Where(entry =>
                string.Equals(
                    entry.SelectToken("content.entryType")?.Value<string>(),
                    "TimelineTimelineItem",
                    StringComparison.Ordinal
                )
            )
            .Where(HasTimelineTweet)
            .Select(CloneObject)
            .ToList();

        return items.Count == 0 ? null : items;
    }

    private static IReadOnlyList<JObject>? GetTimelineModuleItems(JObject root)
    {
        JArray entries = GetEntriesArray(root);

        List<JObject> modules = entries
            .OfType<JObject>()
            .Where(entry =>
                string.Equals(
                    entry.SelectToken("content.entryType")?.Value<string>(),
                    "TimelineTimelineModule",
                    StringComparison.Ordinal
                )
            )
            .ToList();

        if (modules.Count == 0)
            return null;

        if (modules.Count > 1)
            throw new Exception();

        JArray items = modules[0].SelectToken("content.items") as JArray ?? throw new Exception();

        return items
            .OfType<JObject>()
            .Where(HasModuleItemTweet)
            .Select(ToTimelineContentEntry)
            .ToList();
    }

    private static IReadOnlyList<JObject>? GetModuleItems(JObject root)
    {
        if (root.SelectToken("..moduleItems") is not JArray items)
            return null;

        List<JObject> entries = items
            .OfType<JObject>()
            .Where(HasModuleItemTweet)
            .Select(ToTimelineContentEntry)
            .ToList();

        return entries.Count == 0 ? null : entries;
    }

    private static JArray GetEntriesArray(JObject root) =>
        root.SelectToken("..entries") as JArray ?? throw new Exception();

    private static bool HasTimelineTweet(JObject entry) =>
        entry.SelectToken("content.itemContent.tweet_results.result.core") is not null
        || entry.SelectToken("content.itemContent.tweet_results.result.tweet") is not null;

    private static bool HasModuleItemTweet(JObject entry) =>
        entry.SelectToken("item.itemContent.tweet_results.result.core") is not null
        || entry.SelectToken("item.itemContent.tweet_results.result.tweet") is not null;

    private static JObject ToTimelineContentEntry(JObject source)
    {
        source = (JObject)source.DeepClone();
        JObject content = source["content"] as JObject ?? new JObject();
        content["entryType"] = "";
        content["itemContent"] = source.SelectToken("item.itemContent") ?? throw new Exception();
        source["content"] = content;
        source["item"] = null;
        return source;
    }

    private static JObject CloneObject(JObject source) => (JObject)source.DeepClone();
}
