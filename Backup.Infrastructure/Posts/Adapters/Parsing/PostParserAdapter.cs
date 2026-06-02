using Backup.Application.Posts;
using Backup.Application.Posts.Models;
using Backup.Infrastructure.Posts.Abstractions.Services;
using Backup.Infrastructure.Posts.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ParseResult = Backup.Application.Posts.Models.ParsedPostBatch;
using ParseUser = Backup.Domain.Posts.ParseUser;
using PostMapper = Backup.Infrastructure.Posts.Adapters.ProjectionMapping.PostMapper;
using PostUser = Backup.Domain.Posts.PostUser;

namespace Backup.Infrastructure.Posts.Adapters;

public class PostParser(
    ILogger<PostParser> _logger,
    IPostTimelineExtractionService postTimelineExtractionService,
    IPostUserParsePolicyService postUserParsePolicyService,
    IPostProjectionParseService postProjectionParseService,
    IPostTokenMaterializationService postTokenMaterializationService
) : IPostParser
{
    private readonly ILogger<PostParser> _logger = _logger;
    private readonly IPostTimelineExtractionService _postTimelineExtractionService =
        postTimelineExtractionService;
    private readonly IPostUserParsePolicyService _postUserParsePolicyService =
        postUserParsePolicyService;
    private readonly IPostProjectionParseService _postProjectionParseService =
        postProjectionParseService;
    private readonly IPostTokenMaterializationService _postTokenMaterializationService =
        postTokenMaterializationService;

    public ParseResult Parse(string userId, string origin, string response)
    {
        JObject root = JObject.Parse(response);
        PostTokenMaterializationBatchResult<Entry> materialized =
            _postTokenMaterializationService.MaterializeMany<Entry>(
                _postTimelineExtractionService.ExtractEntries(root)
            );

        foreach (string error in materialized.Errors)
            _logger.LogError("Error: {error}", error);

        PostProjectionParseBatchResult batch = _postProjectionParseService.Parse(
            materialized.Items,
            PostMapper.Map
        );

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
            throw new FormatException("User parse payload is missing 'data'.");

        DataUser? data = _postTokenMaterializationService.Materialize<DataUser>(token);

        if (data is null)
            throw new FormatException("User parse payload could not be materialized.");

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
            throw new FormatException("User parse payload is invalid");

        return new ParseUser(user);
    }
}
