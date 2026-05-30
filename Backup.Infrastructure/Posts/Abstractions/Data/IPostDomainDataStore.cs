using Backup.Domain.Posts;

namespace Backup.Infrastructure.Posts.Abstractions.Data;

public interface IPostDomainDataStore : IPostDomainData
{
    bool IsDefault { get; }
    Task<PostStoreCounts> GetStoreCounts();
}
