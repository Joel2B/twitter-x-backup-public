namespace Backup.Application.Core;

public interface ISecondaryStoreSelectionService
{
    IReadOnlyList<T> SelectSecondaries<T>(IReadOnlyList<T> stores, T primary);
}
