namespace Backup.Application.Core;

public sealed class StorageRegistrationPolicyService : IStorageRegistrationPolicyService
{
    public IReadOnlyList<T> SelectEnabled<T>(
        IEnumerable<T> storages,
        Func<T, bool> isEnabled,
        Func<T, bool> isSupportedType,
        Func<T, bool> isDefault
    )
    {
        List<T> enabled = storages
            .Where(storage => isEnabled(storage) && isSupportedType(storage))
            .ToList();

        int defaultCount = enabled.Count(isDefault);

        if (enabled.Count > 0 && defaultCount == 0)
        {
            throw new InvalidOperationException(
                "At least one enabled storage must have Default=true in the same data collection."
            );
        }

        if (defaultCount > 1)
        {
            throw new InvalidOperationException(
                "Only one enabled storage can have Default=true in the same data collection."
            );
        }

        return enabled;
    }
}
