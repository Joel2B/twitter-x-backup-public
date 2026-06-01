using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public sealed class PostMetaNormalizationService : IPostMetaNormalizationService
{
    public IReadOnlyDictionary<string, PostMetaRecord> Normalize(
        IReadOnlyCollection<PostMetaRecord> entries
    )
    {
        if (entries.Count == 0)
            return new Dictionary<string, PostMetaRecord>(StringComparer.Ordinal);

        return entries
            .Where(entry =>
                !string.IsNullOrWhiteSpace(entry.Id) && !string.IsNullOrWhiteSpace(entry.Hash)
            )
            .GroupBy(entry => entry.Id, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToDictionary(entry => entry.Id, entry => entry, StringComparer.Ordinal);
    }
}
