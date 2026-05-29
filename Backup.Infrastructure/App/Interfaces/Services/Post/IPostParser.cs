using Backup.App.Models.Posts;

namespace Backup.App.Interfaces.Services.Posts;

public interface IPostParser
{
    public ParseResult Parse(string userId, string origin, string response);
    public ParseUser ParseUser(string response);
}
