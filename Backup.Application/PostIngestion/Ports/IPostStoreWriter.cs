using Backup.Domain.Posts;

namespace Backup.Application.PostIngestion.Ports;

public interface IPostStoreWriter
{
    Task<int> GetCount();

    Task AddPosts(string userId, string origin, List<Post> posts);

    Task Save();
}
