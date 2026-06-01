using Backup.Application.Posts.Ports;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Posts.Adapters;

public sealed class PostRecoveryCommandAdapter(
    ILogger logger,
    AppConfig config,
    IMediaLogger mediaLogger,
    IPostDownloader downloader,
    IPostDomainParser parser,
    IPostTweetDetailRequestFactory tweetDetailRequestFactory,
    IPostDomainData postData,
    UsersContext context,
    string recoveryOrigin
) : IPostRecoveryCommand
{
    public IPostRecoverySession CreateSession() =>
        new PostRecoverySessionAdapter(
            logger,
            config,
            mediaLogger,
            downloader,
            parser,
            tweetDetailRequestFactory,
            context,
            recoveryOrigin
        );

    public async Task MergeRecoveredPosts(IReadOnlyCollection<Backup.Domain.Posts.Post> posts)
    {
        await postData.AddPosts(
            context.UserId,
            recoveryOrigin,
            [.. posts],
            new() { Index = false }
        );
    }

    public async Task SavePosts()
    {
        logger.LogInformation("saving posts");
        await postData.Save();
    }

    public void OnNoPostsRecovered() => logger.LogInformation("recovery has no posts to merge");

    public void OnPostsMerged(int count) => logger.LogInformation("post {post} merged", count);

    public void OnError(Exception exception) =>
        logger.LogError("Error: {error}", JsonConvert.SerializeObject(exception));
}
