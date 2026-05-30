using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Request;

namespace Backup.Infrastructure.Posts.Abstractions.Services;

public interface IPostTweetDetailRequestFactory
{
    Request? Build(UsersContext context);
}
