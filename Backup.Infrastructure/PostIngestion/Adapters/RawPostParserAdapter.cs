using Backup.Application.PostIngestion.Models;
using Backup.Application.PostIngestion.Ports;
using Backup.Infrastructure.Posts.Abstractions.Services;
using ParseResult = Backup.Domain.Posts.ParseResult;

namespace Backup.Infrastructure.PostIngestion.Adapters;

public class RawPostParserAdapter(IPostDomainParser postParser) : IRawPostParser
{
    private readonly IPostDomainParser _postParser = postParser;

    public RawPostParseResult Parse(string userId, string origin, string rawRequestBody)
    {
        ParseResult parsed = _postParser.Parse(userId, origin, rawRequestBody);
        return new RawPostParseResult(parsed.Posts, parsed.NextCursor);
    }
}
