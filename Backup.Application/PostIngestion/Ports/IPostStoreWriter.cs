using Backup.Domain.Posts;

namespace Backup.Application.PostIngestion.Ports;

public interface IPostStoreWriter
{
    Task<int> GetCount(CancellationToken cancellationToken = default);

    Task AddPosts(
        string userId,
        string origin,
        List<Post> posts,
        CancellationToken cancellationToken = default
    );

    Task Save(CancellationToken cancellationToken = default);
}
