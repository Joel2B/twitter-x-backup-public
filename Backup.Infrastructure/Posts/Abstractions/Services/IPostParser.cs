using Backup.Infrastructure.Models.Posts;

namespace Backup.Infrastructure.Interfaces.Services.Posts;

public interface IPostParser
{
    public ParseResult Parse(string userId, string origin, string response);
    public ParseUser ParseUser(string response);
}

