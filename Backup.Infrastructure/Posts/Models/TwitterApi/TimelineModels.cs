using Newtonsoft.Json;

namespace Backup.Infrastructure.Posts.Models;

public class Content
{
    [JsonProperty("entryType", NullValueHandling = NullValueHandling.Ignore)]
    public required string EntryType { get; set; }

    [JsonProperty("__typename", NullValueHandling = NullValueHandling.Ignore)]
    public string? Typename { get; set; }

    [JsonProperty("items", NullValueHandling = NullValueHandling.Ignore)]
    public List<Entry>? Items { get; set; }

    [JsonProperty("itemContent", NullValueHandling = NullValueHandling.Ignore)]
    public required ItemContent ItemContent { get; set; }

    [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)]
    public string? Value { get; set; }

    [JsonProperty("cursorType", NullValueHandling = NullValueHandling.Ignore)]
    public string? CursorType { get; set; }
}

public class Item
{
    [JsonProperty("itemContent", NullValueHandling = NullValueHandling.Ignore)]
    public ItemContent? ItemContent { get; set; }
}

public class Entry
{
    [JsonProperty("entryId", NullValueHandling = NullValueHandling.Ignore)]
    public required string EntryId { get; set; }

    [JsonProperty("item", NullValueHandling = NullValueHandling.Ignore)]
    public Item? Item { get; set; }

    [JsonProperty("sortIndex", NullValueHandling = NullValueHandling.Ignore)]
    public string? SortIndex { get; set; }

    [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
    public required Content Content { get; set; }
}

public class Instruction
{
    [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
    public string? Type { get; set; }

    [JsonProperty("entries", NullValueHandling = NullValueHandling.Ignore)]
    public required List<Entry> Entries { get; set; }
}

public class ItemContent
{
    [JsonProperty("itemType", NullValueHandling = NullValueHandling.Ignore)]
    public string? ItemType { get; set; }

    [JsonProperty("__typename", NullValueHandling = NullValueHandling.Ignore)]
    public string? Typename { get; set; }

    [JsonProperty("tweet_results", NullValueHandling = NullValueHandling.Ignore)]
    public TweetResults? TweetResults { get; set; }

    [JsonProperty("tweetDisplayType", NullValueHandling = NullValueHandling.Ignore)]
    public string? TweetDisplayType { get; set; }
}

public class Metadata
{
    [JsonProperty("scribeConfig", NullValueHandling = NullValueHandling.Ignore)]
    public ScribeConfig? ScribeConfig { get; set; }
}

public class ScribeConfig
{
    [JsonProperty("page", NullValueHandling = NullValueHandling.Ignore)]
    public string? Page { get; set; }
}

public class Timeline
{
    [JsonProperty("instructions", NullValueHandling = NullValueHandling.Ignore)]
    public required List<Instruction> Instructions { get; set; }

    [JsonProperty("metadata", NullValueHandling = NullValueHandling.Ignore)]
    public Metadata? Metadata { get; set; }
}

public class TimelineV2
{
    [JsonProperty("timeline", NullValueHandling = NullValueHandling.Ignore)]
    public required Timeline Timeline { get; set; }
}

public class TweetResults
{
    [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
    public required Result Result { get; set; }
}
