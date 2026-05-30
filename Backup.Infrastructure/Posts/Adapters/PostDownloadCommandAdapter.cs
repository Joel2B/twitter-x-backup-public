using Backup.Application.Posts.Ports;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Models.Config.Api;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Adapters;

public sealed class PostDownloadCommandAdapter(
    ILogger logger,
    IPostDownloader downloader,
    IPostLogger postLogger,
    IPostDomainParser parser,
    Backup.Infrastructure.Interfaces.Data.Dump.IDumpData dump,
    IPostDomainData postData,
    ApiContext context
) : IPostDownloadCommand
{
    public Task<int> GetLoadedCount() => postData.GetCount();

    public IPostDownloadSession CreateSession() =>
        new PostDownloadSessionAdapter(logger, downloader, postLogger, parser, dump, postData, context);

    public void OnLoadedCount(int count) =>
        logger.LogInformation("download loaded {count} posts", count);

    public void OnError(Exception exception) => logger.LogError("Error: {error}", exception.Message);

    public Task PruneLogs() => postLogger.Prune();

    public async Task SavePosts()
    {
        logger.LogInformation("saving posts");
        await postData.Save();
    }
}
