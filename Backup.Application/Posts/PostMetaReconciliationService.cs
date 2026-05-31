using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public sealed class PostMetaReconciliationService : IPostMetaReconciliationService
{
    public IReadOnlyDictionary<string, PostMetaRecord> Reconcile(
        IReadOnlyDictionary<string, PostMetaRecord> existing,
        IReadOnlyCollection<PostMetaRecord> current
    )
    {
        Dictionary<string, PostMetaRecord> reconciled = existing.ToDictionary(
            entry => entry.Key,
            entry =>
                new PostMetaRecord
                {
                    Id = entry.Value.Id,
                    Hash = entry.Value.Hash,
                    Deleted = entry.Value.Deleted,
                },
            StringComparer.Ordinal
        );

        HashSet<string> currentIds = [];

        foreach (PostMetaRecord item in current)
        {
            currentIds.Add(item.Id);
            reconciled[item.Id] = new PostMetaRecord
            {
                Id = item.Id,
                Hash = item.Hash,
                Deleted = item.Deleted,
            };
        }

        List<string> staleIds = [.. reconciled.Keys.Where(id => !currentIds.Contains(id))];

        foreach (string staleId in staleIds)
            reconciled.Remove(staleId);

        return reconciled;
    }
}
