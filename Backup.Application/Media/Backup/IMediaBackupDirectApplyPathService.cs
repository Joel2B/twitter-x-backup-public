namespace Backup.Application.Media.Backup;

public interface IMediaBackupDirectApplyPathService
{
    IReadOnlyList<string> GetPaths(IEnumerable<string> directPaths);
}
