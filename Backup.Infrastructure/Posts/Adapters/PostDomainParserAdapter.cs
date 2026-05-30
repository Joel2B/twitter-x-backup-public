using Backup.Application.Posts;
using Backup.Domain.Posts;
using Backup.Infrastructure.Interfaces.Services.Posts;

namespace Backup.Infrastructure.Posts.Adapters;

public sealed class PostDomainParserAdapter(
    IPostParser parser,
    IPostProjectionComposer projectionComposer,
    IPostIndexingService postIndexingService
)
    : IPostDomainParser
{
    private readonly IPostParser _parser = parser;
    private readonly IPostProjectionComposer _projectionComposer = projectionComposer;
    private readonly IPostIndexingService _postIndexingService = postIndexingService;

    public ParseResult Parse(string userId, string origin, string response)
    {
        Backup.Application.Posts.Models.ParsedPostBatch parsed = _parser.Parse(
            userId,
            origin,
            response
        );
        List<Post> posts = _projectionComposer.ComposeMany(parsed.Posts);
        _postIndexingService.ApplySequenceIndex(posts, userId, origin);
        return new ParseResult(posts, parsed.NextCursor);
    }

    public ParseUser ParseUser(string response)
        => _parser.ParseUser(response);
}
