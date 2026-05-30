using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Posts.Ports;

public interface IPostRecoverySession
{
    bool RecoveryEnabled { get; }
    int MaxRecoveryPosts { get; }
    bool CanDownloadTweetDetail { get; }
    Task<IReadOnlyCollection<PostRecoveryLog>> GetRecoveryLogs();
    Task<Post?> DownloadPost(string postId, CancellationToken cancellationToken);
    Task MarkRecovered(string postId);
    Task DelayBetweenDownloads(CancellationToken cancellationToken);
    void OnRecoveryDisabled();
    void OnSelectedPosts(int count);
    void OnTweetDetailUnavailable();
    void OnRecoveredPost(string postId);
}

