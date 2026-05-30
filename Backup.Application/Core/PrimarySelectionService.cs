namespace Backup.Application.Core;

public sealed class PrimarySelectionService : IPrimarySelectionService
{
    public T ResolvePrimary<T>(
        IReadOnlyList<T> items,
        Func<T, bool> isDefault,
        string noItemsMessage,
        string multipleDefaultsMessage
    )
    {
        if (items.Count == 0)
            throw new InvalidOperationException(noItemsMessage);

        List<T> defaults = items.Where(isDefault).ToList();

        if (defaults.Count > 1)
            throw new InvalidOperationException(multipleDefaultsMessage);

        if (defaults.Count > 0)
            return defaults[0];

        return items[0];
    }
}
