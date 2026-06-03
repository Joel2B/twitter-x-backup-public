namespace Backup.Application.Posts;

public sealed class PostLogFolderPolicyService : IPostLogFolderPolicyService
{
    public string CreateSessionFolderName(DateTime now) => now.ToString("yyyy.MM.dd-HH.mm.ss");
}
