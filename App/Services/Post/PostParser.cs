using AutoMapper;
using Backup.App.Interfaces.Services.Post;
using Backup.App.Models.Post;
using Backup.App.Models.Post.Response;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Backup.App.Services.Post;

public class PostParser(ILogger<PostParser> _logger, IMapper _mapper) : IPostParser
{
    private readonly ILogger<PostParser> _logger = _logger;
    private readonly IMapper _mapper = _mapper;

    public ParseResult Parse(string userId, string origin, string response)
    {
        List<Entry>? entries = GetEntries(response);

        if (entries is null || entries.Count == 0)
            entries = GetEntriesModule(response);

        if (entries is null || entries.Count == 0)
            entries = GetEntriesModuleItems(response);

        if (entries is null)
            entries = [];

        List<Models.Post.Post> tweets = [];
        List<Entry> debugTweets = [];

        foreach (Entry entry in entries)
            try
            {
                Models.Post.Post post = _mapper.Map<Models.Post.Post>(entry);
                tweets.Add(post);
            }
            catch (Exception ex)
            {
                debugTweets.Add(entry);

                _logger.LogError(
                    "Error: {message}, {exception}",
                    ex.Message,
                    JsonConvert.SerializeObject(
                        ex,
                        new JsonSerializerSettings
                        {
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                        }
                    )
                );
            }

        for (int i = 0; i < tweets.Count; i++)
        {
            IndexData index = new()
            {
                Previous = i == 0 ? null : tweets[i - 1].Id,
                Next = i == tweets.Count - 1 ? null : tweets[i + 1].Id,
            };

            tweets[i].Index[userId] = [];
            tweets[i].Index[userId][origin] = index;
        }

        string? cursor = GetCursor(response);

        return new ParseResult(tweets, cursor);
    }

    private static List<Entry>? GetEntries(string response)
    {
        JObject root = JObject.Parse(response);
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

        List<Entry> entriesTweets = entriesItem
            .Where(e =>
                e.Content?.ItemContent?.TweetResults?.Result?.Core is not null
                || e.Content?.ItemContent?.TweetResults?.Result?.Tweet is not null
            )
            .ToList();

        return entriesTweets;
    }

    private string? GetCursor(string response)
    {
        JObject root = JObject.Parse(response);
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

        string? cursor = entriesCursor
            .Where(cursor => cursor.Content.CursorType == "Bottom")
            .FirstOrDefault()
            ?.Content.Value;

        return cursor;
    }

    private List<Entry>? GetEntriesModule(string response)
    {
        JObject root = JObject.Parse(response);
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

        if (entriesModule is null || entriesModule.Count > 1)
            throw new Exception();

        List<Entry>? items = entriesModule[0].Content.Items;

        if (items is null)
            throw new Exception();

        List<Entry> entriesTweets = items
            .Where(e =>
                e.Item?.ItemContent?.TweetResults?.Result?.Core is not null
                || e.Item?.ItemContent?.TweetResults?.Result?.Tweet is not null
            )
            .Select(o =>
            {
                o.Content = new()
                {
                    EntryType = "",
                    ItemContent = o.Item?.ItemContent ?? throw new Exception("Item is null"),
                };

                o.Item = null;

                return o;
            })
            .ToList();

        return entriesTweets;
    }

    private List<Entry>? GetEntriesModuleItems(string response)
    {
        JObject root = JObject.Parse(response);
        JToken? token = root.SelectToken("..moduleItems");

        if (token is not JArray tokenArray)
            return null;

        List<Entry>? entries = tokenArray.ToObject<List<Entry>>();

        if (entries is null)
            return null;

        List<Entry> entriesTweets = entries
            .Where(e =>
                e.Item?.ItemContent?.TweetResults?.Result?.Core is not null
                || e.Item?.ItemContent?.TweetResults?.Result?.Tweet is not null
            )
            .Select(o =>
            {
                o.Content = new()
                {
                    EntryType = "",
                    ItemContent = o.Item?.ItemContent ?? throw new Exception("Item is null"),
                };

                o.Item = null;

                return o;
            })
            .ToList();

        return entriesTweets;
    }

    public ParseUser ParseUser(string response)
    {
        JObject root = JObject.Parse(response);
        JToken? token = root.SelectToken("data");

        if (token is null)
            throw new Exception();

        Models.Post.Response.Data? data = token.ToObject<Models.Post.Response.Data>();

        if (data is null)
            throw new Exception();

        if (data.User is null)
            return new ParseUser(null);

        if (data.User.Result.Typename == "UserUnavailable")
        {
            _logger.LogInformation("{message}", data.User.Result.Message);
            return new ParseUser(null);
        }

        Models.Post.User user = new()
        {
            Id = data.User.Result.RestId ?? throw new Exception("RestId is null"),
            MediaCount =
                data.User.Result.Legacy?.MediaCount ?? throw new Exception("MediaCount is null"),
        };

        return new ParseUser(user);
    }
}
