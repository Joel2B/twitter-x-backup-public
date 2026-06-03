namespace Backup.Application.Media;

public interface IMediaPathSelectionService
{
    string SelectRequiredRootPath(IEnumerable<string> rootPaths);
}
