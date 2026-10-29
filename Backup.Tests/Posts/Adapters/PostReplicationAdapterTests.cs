using Backup.Application.Posts;
using Backup.Application.Posts.Ports;
using Backup.Infrastructure.Posts.Adapters;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backup.Tests;

public sealed class PostReplicationAdapterTests
{
    [Fact]
    public async Task Replicate_PropagatesServiceException()
    {
        InvalidOperationException expected = new("replication failed");
        PostReplicationAdapter sut = new(
            new ThrowingPostReplicationService(expected),
            NullLogger<PostReplicationAdapter>.Instance
        );

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.Replicate([])
        );

        Assert.Same(expected, actual);
    }

    private sealed class ThrowingPostReplicationService(Exception exception)
        : IPostReplicationService
    {
        public Task Replicate(IEnumerable<IPostReplicationStore> stores) =>
            Task.FromException(exception);
    }
}
