using Backup.Application.Posts.Ports;

namespace Backup.Application.Posts;

public sealed class PostDownloadCommandService(
    IPostDownloadOrchestrationService postDownloadOrchestrationService
) : IPostDownloadCommandService
{
    private readonly IPostDownloadOrchestrationService _postDownloadOrchestrationService =
        postDownloadOrchestrationService;

    public async Task Execute(IPostDownloadCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        int count = await command.GetLoadedCount();
        command.OnLoadedCount(count);

        try
        {
            await _postDownloadOrchestrationService.Run(command.CreateSession(), cancellationToken);
        }
        catch (Exception ex)
        {
            command.OnError(ex);
        }
        finally
        {
            await command.PruneLogs();
        }

        await command.SavePosts();
    }
}
