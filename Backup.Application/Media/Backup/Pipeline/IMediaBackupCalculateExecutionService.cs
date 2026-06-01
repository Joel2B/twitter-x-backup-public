using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupCalculateExecutionService
{
    MediaBackupCalculateExecutionResult Execute(MediaBackupCalculateExecutionInput input);
}
