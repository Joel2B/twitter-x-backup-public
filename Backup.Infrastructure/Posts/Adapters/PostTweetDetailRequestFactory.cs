using Backup.Application.Config;
using Backup.Infrastructure.Adapters;
using Backup.Infrastructure.Posts.Abstractions.Services;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Request;

namespace Backup.Infrastructure.Posts.Adapters;

public sealed class PostTweetDetailRequestFactory(IApiRequestBuildService apiRequestBuildService)
    : IPostTweetDetailRequestFactory
{
    private readonly IApiRequestBuildService _apiRequestBuildService = apiRequestBuildService;

    public Request? Build(UsersContext context)
    {
        var built = _apiRequestBuildService.Build(
            ApiRequestBuildMapper.ToSources(context.Api),
            "TweetDetail"
        );

        return built is null ? null : ApiRequestBuildMapper.ToRequest(built);
    }
}
