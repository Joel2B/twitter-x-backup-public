using Backup.Application.PostIngestion.Models;
using Backup.Application.PostIngestion.Ports;
using Backup.App.Interfaces.Services.Posts;

namespace Backup.Infrastructure.PostIngestion.Adapters;

public class RawPostParserAdapter(IPostParser postParser) : IRawPostParser
{
    private readonly IPostParser _postParser = postParser;

    public RawPostParseResult Parse(string userId, string origin, string rawRequestBody)
    {
        Backup.App.Models.Posts.ParseResult parsed = _postParser.Parse(userId, origin, rawRequestBody);
        return new RawPostParseResult(parsed.Posts.Select(PostDomainMapper.ToDomain).ToList(), parsed.NextCursor);
    }
}
