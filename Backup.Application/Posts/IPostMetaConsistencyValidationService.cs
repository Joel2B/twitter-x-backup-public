namespace Backup.Application.Posts;

public interface IPostMetaConsistencyValidationService
{
    void EnsureAligned(IEnumerable<string> postIds, IEnumerable<string> metaIds, string storeLabel);
}
