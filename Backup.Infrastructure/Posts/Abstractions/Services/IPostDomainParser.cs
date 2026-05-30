using Backup.Domain.Posts;

namespace Backup.Infrastructure.Posts.Abstractions.Services;

public interface IPostDomainParser
{
    public ParseResult Parse(string userId, string origin, string response);
    public ParseUser ParseUser(string response);
}
