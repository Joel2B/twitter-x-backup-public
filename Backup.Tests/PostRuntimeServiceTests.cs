using Backup.Application.Posts;
using Backup.Application.Posts.Ports;

namespace Backup.Tests;

public class PostRuntimeServiceTests
{
    private readonly IPostRuntimeService _service = new PostRuntimeService(new PostExecutionService());

    [Fact]
    public async Task Download_CallsStartThenDownloadThenPrune()
    {
        List<string> calls = [];
        IPostDownloadRuntimeCommand command = new FakeDownloadRuntimeCommand(calls);

        await _service.Download(command);

        Assert.Equal(["start", "download", "prune"], calls);
    }

    [Fact]
    public async Task Recover_CallsStartThenRecover()
    {
        List<string> calls = [];
        IPostRecoveryRuntimeCommand command = new FakeRecoveryRuntimeCommand(calls);

        await _service.Recover(command);

        Assert.Equal(["start", "recover"], calls);
    }

    private sealed class FakeDownloadRuntimeCommand(List<string> calls) : IPostDownloadRuntimeCommand
    {
        public void OnDownloadStarting() => calls.Add("start");

        public Task RunDownload()
        {
            calls.Add("download");
            return Task.CompletedTask;
        }

        public Task RunPrune()
        {
            calls.Add("prune");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRecoveryRuntimeCommand(List<string> calls)
        : IPostRecoveryRuntimeCommand
    {
        public void OnRecoveryStarting() => calls.Add("start");

        public Task RunRecovery()
        {
            calls.Add("recover");
            return Task.CompletedTask;
        }
    }
}

