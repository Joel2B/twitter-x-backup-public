using Backup.App.Models.Post;

namespace Backup.App.Interfaces.Services.Post;

public interface IPostParser
{
    public ParseResult Parse(string userId, string origin, string response);
    public ParseUser ParseUser(string response);
}
