namespace Backup.Application.Posts;

public sealed class PostMetaConsistencyValidationService : IPostMetaConsistencyValidationService
{
    public void EnsureAligned(IEnumerable<string> postIds, IEnumerable<string> metaIds, string storeLabel)
    {
        HashSet<string> postIdSet = postIds.ToHashSet(StringComparer.Ordinal);
        HashSet<string> metaIdSet = metaIds.ToHashSet(StringComparer.Ordinal);

        if (postIdSet.SetEquals(metaIdSet))
            return;

        int missingInMeta = postIdSet.Count(id => !metaIdSet.Contains(id));
        int missingInPosts = metaIdSet.Count(id => !postIdSet.Contains(id));

        throw new Exception(
            $"post_meta is out of sync in store '{storeLabel}'. missingInMeta={missingInMeta}, missingInPosts={missingInPosts}"
        );
    }
}
