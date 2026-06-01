using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public interface IPostMetaReconciliationService
{
    IReadOnlyDictionary<string, PostMetaRecord> Reconcile(
        IReadOnlyDictionary<string, PostMetaRecord> existing,
        IReadOnlyCollection<PostMetaRecord> current
    );
}
