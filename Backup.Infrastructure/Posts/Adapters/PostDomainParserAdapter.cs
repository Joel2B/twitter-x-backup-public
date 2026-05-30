using Backup.Infrastructure.Interfaces.Services.Posts;

namespace Backup.Infrastructure.Posts.Adapters;

public sealed class PostDomainParserAdapter(IPostParser parser) : IPostDomainParser
{
    private readonly IPostParser _parser = parser;

    public Backup.Infrastructure.Models.Posts.DomainParseResult Parse(
        string userId,
        string origin,
        string response
    )
    {
        Backup.Infrastructure.Models.Posts.ParseResult parsed = _parser.Parse(userId, origin, response);
        return new Backup.Infrastructure.Models.Posts.DomainParseResult(
            parsed.Posts.Select(PostReplicationMapper.ToDomain).ToList(),
            parsed.NextCursor
        );
    }

    public Backup.Infrastructure.Models.Posts.ParseUser ParseUser(string response) =>
        _parser.ParseUser(response);
}
