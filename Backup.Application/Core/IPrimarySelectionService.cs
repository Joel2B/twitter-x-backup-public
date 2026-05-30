namespace Backup.Application.Core;

public interface IPrimarySelectionService
{
    T ResolvePrimary<T>(
        IReadOnlyList<T> items,
        Func<T, bool> isDefault,
        string noItemsMessage,
        string multipleDefaultsMessage
    );
}
