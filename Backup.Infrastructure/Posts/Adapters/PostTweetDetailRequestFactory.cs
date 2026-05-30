using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.ApiRequest;

namespace Backup.Infrastructure.Posts.Adapters;

public sealed class PostTweetDetailRequestFactory : IPostTweetDetailRequestFactory
{
    public Request? Build(UsersContext context) =>
        RequestMerge.Build(context.Api, "TweetDetail");
}

