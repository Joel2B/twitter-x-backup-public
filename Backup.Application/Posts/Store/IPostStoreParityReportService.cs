using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public interface IPostStoreParityReportService
{
    PostStoreParityReport Build(PostStoreParityResult parity);
}
