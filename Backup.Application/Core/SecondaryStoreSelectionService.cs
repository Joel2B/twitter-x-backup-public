namespace Backup.Application.Core;

public sealed class SecondaryStoreSelectionService : ISecondaryStoreSelectionService
{
    public IReadOnlyList<T> SelectSecondaries<T>(IReadOnlyList<T> stores, T primary) =>
        stores.Where(store => !ReferenceEquals(store, primary)).ToList();
}
