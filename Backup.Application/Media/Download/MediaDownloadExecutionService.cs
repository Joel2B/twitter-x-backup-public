using Backup.Application.Media.Models;
using Backup.Application.Media.Ports;

namespace Backup.Application.Media;

public sealed class MediaDownloadExecutionService : IMediaDownloadExecutionService
{
    public async Task Run(
        IMediaDownloadExecutionCommand command,
        IMediaDownloadParallelRunner runner,
        IReadOnlyList<MediaDownloadQueueItem> queue,
        MediaParallelDownloadSettings settings,
        CancellationToken cancellationToken = default
    )
    {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken
        );

        try
        {
            await runner.Run(
                queue,
                settings,
                async (item, token) =>
                {
                    try
                    {
                        using Stream stream = await command.Download(item, token);
                        await command.Save(item, stream, token);
                        command.OnSuccess(item);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        if (command.ShouldCancelOnItemError(ex))
                        {
                            if (!cts.IsCancellationRequested)
                                cts.Cancel();

                            return;
                        }

                        command.OnItemError(item, ex.Message);
                    }
                },
                command.OnDebug,
                cts.Token
            );
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            command.OnFatalError(ex.Message);
        }
        finally
        {
            await command.SaveState();
        }

        await command.SaveLogs();
    }
}
