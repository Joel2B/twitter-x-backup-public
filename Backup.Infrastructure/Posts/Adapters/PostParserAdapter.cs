using Backup.Application.Posts.Models;
using Backup.Application.Posts;
using Backup.Infrastructure.Posts.Abstractions.Services;
using PostMapper = Backup.Infrastructure.Posts.Adapters.ProjectionMapping.PostMapper;
using Backup.Infrastructure.Posts.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ParseResult = Backup.Application.Posts.Models.ParsedPostBatch;
using ParseUser = Backup.Domain.Posts.ParseUser;
using PostUser = Backup.Domain.Posts.PostUser;

namespace Backup.Infrastructure.Posts.Adapters;

public class PostParser(
    ILogger<PostParser> _logger,
    IPostTimelineExtractionService postTimelineExtractionService,
    IPostUserParsePolicyService postUserParsePolicyService,
    IPostProjectionParseService postProjectionParseService
) : IPostParser
{
    private readonly ILogger<PostParser> _logger = _logger;
    private readonly IPostTimelineExtractionService _postTimelineExtractionService =
        postTimelineExtractionService;
    private readonly IPostUserParsePolicyService _postUserParsePolicyService =
        postUserParsePolicyService;
    private readonly IPostProjectionParseService _postProjectionParseService =
        postProjectionParseService;

    public ParseResult Parse(string userId, string origin, string response)
    {
        JObject root = JObject.Parse(response);
        List<Entry> entries = _postTimelineExtractionService
            .ExtractEntries(root)
            .Select(token => token.ToObject<Entry>() ?? throw new Exception())
            .ToList();

        PostProjectionParseBatchResult batch = _postProjectionParseService.Parse(entries, PostMapper.Map);

        foreach (string error in batch.Errors)
            _logger.LogError("Error: {error}", error);

        string? cursor = _postTimelineExtractionService.ExtractCursor(root);

        return new ParseResult(batch.Posts.ToList(), cursor);
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

        if (_postUserParsePolicyService.IsUnavailable(data.User.Result.Typename))
        {
            _logger.LogInformation("{message}", data.User.Result.Message);
            return new ParseUser(null);
        }

        PostUser? user = _postUserParsePolicyService.CreateUser(
            data.User.Result.RestId,
            data.User.Result.Legacy?.MediaCount
        );

        if (user is null)
            throw new Exception("User parse payload is invalid");

        return new ParseUser(user);
    }
}
