namespace Backup.Application.Posts;

public sealed class PostHashMetaParityService : IPostHashMetaParityService
{
    public void EnsureMatch(int postCount, int hashMetaCount, string storeLabel)
    {
        if (postCount == hashMetaCount)
            return;

        throw new InvalidOperationException(
            $"post_meta count mismatch in local post store '{storeLabel}': posts={postCount}, post_meta={hashMetaCount}"
        );
    }
}
