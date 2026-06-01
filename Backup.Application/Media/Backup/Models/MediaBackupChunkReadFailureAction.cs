namespace Backup.Application.Media.Backup.Models;

public enum MediaBackupChunkReadFailureAction
{
    Throw = 0,
    ReturnNull = 1,
}
