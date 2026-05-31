using Backup.Application.Posts.Models;
using Backup.Application.Posts;
using Backup.Infrastructure.Posts.Abstractions.Services;
using PostMapper = Backup.Infrastructure.Posts.Adapters.ProjectionMapping.PostMapper;
using Backup.Infrastructure.Posts.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ParseResult = Backup.Application.Posts.Models.ParsedPostBatch;
using ParseUser = Backup.Domain.Posts.ParseUser;
using PostUser = Backup.Domain.Posts.PostUser;

namespace Backup.Infrastructure.Posts.Adapters;

public class PostParser(
    ILogger<PostParser> _logger,
    IPostTimelineExtractionService postTimelineExtractionService
) : IPostParser
{
    private readonly ILogger<PostParser> _logger = _logger;
    private readonly IPostTimelineExtractionService _postTimelineExtractionService =
        postTimelineExtractionService;

    public ParseResult Parse(string userId, string origin, string response)
    {
        JObject root = JObject.Parse(response);
        List<Entry> entries = _postTimelineExtractionService
            .ExtractEntries(root)
            .Select(token => token.ToObject<Entry>() ?? throw new Exception())
            .ToList();

        List<ParsedPostProjection> tweets = [];
        List<Entry> debugTweets = [];

        foreach (Entry entry in entries)
            try
            {
                ParsedPostProjection post = PostMapper.Map(entry);
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

        string? cursor = _postTimelineExtractionService.ExtractCursor(root);

        return new ParseResult(tweets, cursor);
    }

    public ParseUser ParseUser(string response)
    {
        JObject root = JObject.Parse(response);
        JToken? token = root.SelectToken("data");

        if (token is null)
            throw new Exception();

        DataUser? data = token.ToObject<DataUser>();

        if (data is null)
            throw new Exception();

        if (data.User is null)
            return new ParseUser(null);

        if (data.User.Result.Typename == "UserUnavailable")
        {
            _logger.LogInformation("{message}", data.User.Result.Message);
            return new ParseUser(null);
        }

        PostUser user = new()
        {
            Id = data.User.Result.RestId ?? throw new Exception("RestId is null"),
            MediaCount =
                data.User.Result.Legacy?.MediaCount ?? throw new Exception("MediaCount is null"),
        };

        return new ParseUser(user);
    }
}
