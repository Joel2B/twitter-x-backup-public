namespace Backup.Application.Core;

public interface IStorageRegistrationPolicyService
{
    IReadOnlyList<T> SelectEnabled<T>(
        IEnumerable<T> storages,
        Func<T, bool> isEnabled,
        Func<T, bool> isSupportedType,
        Func<T, bool> isDefault
    );
}
