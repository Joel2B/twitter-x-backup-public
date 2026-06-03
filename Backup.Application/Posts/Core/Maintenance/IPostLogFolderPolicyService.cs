namespace Backup.Application.Posts;

public interface IPostLogFolderPolicyService
{
    string CreateSessionFolderName(DateTime now);
}
