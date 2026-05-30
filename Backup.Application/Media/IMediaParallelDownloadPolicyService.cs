using Backup.Application.Media.Models;

namespace Backup.Application.Media;

public interface IMediaParallelDownloadPolicyService
{
    MediaParallelDownloadSettings Create(int minDegreeOfParallelism, int maxDegreeOfParallelism, int startDegreeOfParallelism);
}
