namespace Backup.Application.Media.Prune;

public interface IMediaPruneSelectionService
{
    bool ShouldRemove(string url, string path);
}
