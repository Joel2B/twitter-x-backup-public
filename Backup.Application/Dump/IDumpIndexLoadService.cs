namespace Backup.Application.Dump;

public interface IDumpIndexLoadService
{
    Task<IReadOnlyList<Backup.Domain.Posts.Post>> LoadPosts(
        IReadOnlyList<string> allPaths,
        IReadOnlyList<string> apiPathSegments
    );
}
