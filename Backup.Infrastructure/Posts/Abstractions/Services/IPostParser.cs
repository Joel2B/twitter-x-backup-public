using ParseResult = Backup.Application.Posts.Models.ParsedPostBatch;
using ParseUser = Backup.Domain.Posts.ParseUser;

namespace Backup.Infrastructure.Posts.Abstractions.Services;

public interface IPostParser
{
    public ParseResult Parse(string userId, string origin, string response);
    public ParseUser ParseUser(string response);
}
