using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Posts.Ports;

public interface IPostStoreCountSource
{
    string Label { get; }
    bool IsDefault { get; }
    Task<PostStoreCounts> GetStoreCounts();
}
