namespace Backup.Application.Media;

public interface IMediaDownloadPathPriorityPolicyService
{
    int GetPriority(string path);
}
