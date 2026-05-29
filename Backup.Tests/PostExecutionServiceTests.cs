using Backup.Application.Posts;
using Backup.Application.Posts.Ports;

namespace Backup.Tests;

public class PostExecutionServiceTests
{
    private readonly IPostExecutionService _service = new PostExecutionService();

    [Fact]
    public async Task Download_RunsDownloadThenPrune()
    {
        List<string> calls = [];
        IPostDownloadExecution execution = new FakeDownloadExecution(calls);

        await _service.Download(execution);

        Assert.Equal(["download", "prune"], calls);
    }

    [Fact]
    public async Task Recover_RunsRecover()
    {
        bool recovered = false;
        IPostRecoveryExecution execution = new FakeRecoveryExecution(() => recovered = true);

        await _service.Recover(execution);

        Assert.True(recovered);
    }

    private sealed class FakeDownloadExecution(List<string> calls) : IPostDownloadExecution
    {
        public Task Download()
        {
            calls.Add("download");
            return Task.CompletedTask;
        }

        public Task Prune()
        {
            calls.Add("prune");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRecoveryExecution(Action callback) : IPostRecoveryExecution
    {
        public Task Recover()
        {
            callback();
            return Task.CompletedTask;
        }
    }
}
