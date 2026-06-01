namespace Backup.Application.Posts;

public interface IPostHashMetaParityService
{
    void EnsureMatch(int postCount, int hashMetaCount, string storeLabel);
}
