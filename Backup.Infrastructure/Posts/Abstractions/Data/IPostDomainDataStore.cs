using Backup.Domain.Posts;

namespace Backup.Infrastructure.Interfaces.Data.Posts;

public interface IPostDomainDataStore : IPostDomainData
{
    bool IsDefault { get; }
    Task<PostStoreCounts> GetStoreCounts();
}
