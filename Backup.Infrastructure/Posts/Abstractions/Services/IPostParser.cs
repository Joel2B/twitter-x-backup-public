using ParseUser = Backup.Domain.Posts.ParseUser;
using ParseResult = Backup.Infrastructure.Models.Posts.ParseResult;

namespace Backup.Infrastructure.Interfaces.Services.Posts;

public interface IPostParser
{
    public ParseResult Parse(string userId, string origin, string response);
    public ParseUser ParseUser(string response);
}

