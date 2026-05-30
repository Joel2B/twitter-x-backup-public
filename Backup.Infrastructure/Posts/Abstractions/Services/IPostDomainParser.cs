using Backup.Infrastructure.Models.Posts;

namespace Backup.Infrastructure.Interfaces.Services.Posts;

public interface IPostDomainParser
{
    public DomainParseResult Parse(string userId, string origin, string response);
    public ParseUser ParseUser(string response);
}
