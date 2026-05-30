namespace Backup.Application.Dump;

public interface IDumpIndexFilePolicyService
{
    IReadOnlyList<string> SelectIndexFiles(
        IEnumerable<string> paths,
        IReadOnlyList<string> apiPathParts
    );
}
