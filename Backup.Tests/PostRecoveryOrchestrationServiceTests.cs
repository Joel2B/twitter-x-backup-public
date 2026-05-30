using Backup.Application.Posts;
using Backup.Application.Posts.Models;
using Backup.Application.Posts.Ports;
using Backup.Domain.Posts;

namespace Backup.Tests;

public class PostRecoveryOrchestrationServiceTests
{
    private readonly IPostRecoveryOrchestrationService _service =
        new PostRecoveryOrchestrationService(new PostRecoverySelectionService());

    [Fact]
    public async Task Recover_WhenDisabled_ReturnsEmptyAndNotifiesDisabled()
    {
        FakeRecoverySession session = new()
        {
            RecoveryEnabledValue = false,
            Logs =
            [
                new PostRecoveryLog { PostId = "1", Messages = ["NotFound"] },
            ],
        };

        IReadOnlyCollection<Post> posts = await _service.Recover(session, CancellationToken.None);

        Assert.Empty(posts);
        Assert.True(session.DisabledNotified);
        Assert.Equal(0, session.DownloadCalls);
    }

    [Fact]
    public async Task Recover_WhenTweetDetailUnavailable_ReturnsEmpty()
    {
        FakeRecoverySession session = new()
        {
            RecoveryEnabledValue = true,
            CanDownloadTweetDetailValue = false,
            Logs =
            [
                new PostRecoveryLog { PostId = "1", Messages = ["NotFound"] },
            ],
        };

        IReadOnlyCollection<Post> posts = await _service.Recover(session, CancellationToken.None);

        Assert.Empty(posts);
        Assert.Equal(1, session.SelectedCount);
        Assert.True(session.UnavailableNotified);
        Assert.Equal(0, session.DownloadCalls);
    }

    [Fact]
    public async Task Recover_DownloadsMarksAndDelays_ForDownloadedPosts()
    {
        FakeRecoverySession session = new()
        {
            RecoveryEnabledValue = true,
            CanDownloadTweetDetailValue = true,
            Logs =
            [
                new PostRecoveryLog { PostId = "1", Messages = ["NotFound"] },
                new PostRecoveryLog { PostId = "2", Messages = ["Forbidden"] },
            ],
            DownloadedByRequestId = new Dictionary<string, Post?>(StringComparer.Ordinal)
            {
                ["1"] = CreatePost("1"),
                ["2"] = null,
            },
        };

        IReadOnlyCollection<Post> posts = await _service.Recover(session, CancellationToken.None);

        Post recovered = Assert.Single(posts);
        Assert.Equal("1", recovered.Id);
        Assert.Equal(2, session.DownloadCalls);
        Assert.Equal(["1"], session.MarkedPostIds);
        Assert.Equal(["1"], session.RecoveredPostNotifications);
        Assert.Equal(1, session.DelayCalls);
    }

    private static Post CreatePost(string id) =>
        new()
        {
            Id = id,
            Profile = new PostProfile { Id = $"profile-{id}", UserName = $"user-{id}" },
            Description = $"desc-{id}",
            Retweeted = false,
            Favorited = false,
            Bookmarked = false,
            CreatedAt = "2025-01-01T00:00:00Z",
            Hashtags = [],
            Medias = [],
        };

    private sealed class FakeRecoverySession : IPostRecoverySession
    {
        public bool RecoveryEnabledValue { get; init; } = true;
        public int MaxRecoveryPosts { get; init; } = 10;
        public bool CanDownloadTweetDetailValue { get; init; } = true;
        public IReadOnlyCollection<PostRecoveryLog> Logs { get; init; } = [];
        public Dictionary<string, Post?> DownloadedByRequestId { get; init; } =
            new(StringComparer.Ordinal);

        public int DownloadCalls { get; private set; }
        public int DelayCalls { get; private set; }
        public bool DisabledNotified { get; private set; }
        public bool UnavailableNotified { get; private set; }
        public int SelectedCount { get; private set; }
        public List<string> MarkedPostIds { get; } = [];
        public List<string> RecoveredPostNotifications { get; } = [];

        public bool RecoveryEnabled => RecoveryEnabledValue;
        public bool CanDownloadTweetDetail => CanDownloadTweetDetailValue;

        public Task<IReadOnlyCollection<PostRecoveryLog>> GetRecoveryLogs() => Task.FromResult(Logs);

        public Task<Post?> DownloadPost(string postId, CancellationToken cancellationToken)
        {
            DownloadCalls++;
            DownloadedByRequestId.TryGetValue(postId, out Post? post);
            return Task.FromResult(post);
        }

        public Task MarkRecovered(string postId)
        {
            MarkedPostIds.Add(postId);
            return Task.CompletedTask;
        }

        public Task DelayBetweenDownloads(CancellationToken cancellationToken)
        {
            DelayCalls++;
            return Task.CompletedTask;
        }

        public void OnRecoveryDisabled() => DisabledNotified = true;

        public void OnSelectedPosts(int count) => SelectedCount = count;

        public void OnTweetDetailUnavailable() => UnavailableNotified = true;

        public void OnRecoveredPost(string postId) => RecoveredPostNotifications.Add(postId);
    }
}
