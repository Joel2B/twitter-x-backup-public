using Backup.Infrastructure.Posts.Models;
using Newtonsoft.Json.Linq;

namespace Backup.Infrastructure.Posts.Adapters.Parsing;

internal static class TimelineEntryExtractor
{
    public static List<Entry> ExtractEntries(JObject root)
    {
        List<Entry>? entries = GetEntries(root);

        if (entries is null || entries.Count == 0)
            entries = GetEntriesModule(root);

        if (entries is null || entries.Count == 0)
            entries = GetEntriesModuleItems(root);

        return entries ?? [];
    }

    public static string? ExtractCursor(JObject root)
    {
        JToken? token = root.SelectToken("..entries");

        if (token is not JArray tokenArray)
            throw new Exception();

        List<Entry>? entries = tokenArray.ToObject<List<Entry>>();

        if (entries is null)
            throw new Exception();

        Dictionary<string, List<Entry>> entriesDict = entries
            .GroupBy(o => o.Content.EntryType)
            .ToDictionary(o => o.Key, o => o.ToList());

        if (!entriesDict.TryGetValue("TimelineTimelineCursor", out List<Entry>? entriesCursor))
            return null;

        return entriesCursor
            .FirstOrDefault(cursor => cursor.Content.CursorType == "Bottom")
            ?.Content.Value;
    }

    private static List<Entry>? GetEntries(JObject root)
    {
        JToken? token = root.SelectToken("..entries");

        if (token is not JArray tokenArray)
            throw new Exception();

        List<Entry>? entries = tokenArray.ToObject<List<Entry>>();

        if (entries is null)
            throw new Exception();

        Dictionary<string, List<Entry>> entriesDict = entries
            .GroupBy(o => o.Content.EntryType)
            .ToDictionary(o => o.Key, o => o.ToList());

        if (!entriesDict.TryGetValue("TimelineTimelineItem", out List<Entry>? entriesItem))
            return null;

        return entriesItem
            .Where(e =>
                e.Content?.ItemContent?.TweetResults?.Result?.Core is not null
                || e.Content?.ItemContent?.TweetResults?.Result?.Tweet is not null
            )
            .ToList();
    }

    private static List<Entry>? GetEntriesModule(JObject root)
    {
        JToken? token = root.SelectToken("..entries");

        if (token is not JArray tokenArray)
            throw new Exception();

        List<Entry>? entries = tokenArray.ToObject<List<Entry>>();

        if (entries is null)
            throw new Exception();

        Dictionary<string, List<Entry>> entriesDict = entries
            .GroupBy(o => o.Content.EntryType)
            .ToDictionary(o => o.Key, o => o.ToList());

        if (!entriesDict.TryGetValue("TimelineTimelineModule", out List<Entry>? entriesModule))
            return null;

        if (entriesModule.Count > 1)
            throw new Exception();

        List<Entry>? items = entriesModule[0].Content.Items;

        if (items is null)
            throw new Exception();

        return items
            .Where(e =>
                e.Item?.ItemContent?.TweetResults?.Result?.Core is not null
                || e.Item?.ItemContent?.TweetResults?.Result?.Tweet is not null
            )
            .Select(ToContentEntry)
            .ToList();
    }

    private static List<Entry>? GetEntriesModuleItems(JObject root)
    {
        JToken? token = root.SelectToken("..moduleItems");

        if (token is not JArray tokenArray)
            return null;

        List<Entry>? entries = tokenArray.ToObject<List<Entry>>();

        if (entries is null)
            return null;

        return entries
            .Where(e =>
                e.Item?.ItemContent?.TweetResults?.Result?.Core is not null
                || e.Item?.ItemContent?.TweetResults?.Result?.Tweet is not null
            )
            .Select(ToContentEntry)
            .ToList();
    }

    private static Entry ToContentEntry(Entry source)
    {
        source.Content = new()
        {
            EntryType = "",
            ItemContent = source.Item?.ItemContent ?? throw new Exception("Item is null"),
        };
        source.Item = null;
        return source;
    }
}
