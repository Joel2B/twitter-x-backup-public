using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.ApiRequest;

namespace Backup.Infrastructure.Interfaces.Services.Posts;

public interface IPostTweetDetailRequestFactory
{
    Request? Build(UsersContext context);
}

