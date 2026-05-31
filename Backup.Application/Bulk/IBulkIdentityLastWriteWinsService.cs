namespace Backup.Application.Bulk;

public interface IBulkIdentityLastWriteWinsService
{
    IReadOnlyDictionary<string, int> BuildLastIndexByKey(IEnumerable<string> keys);
}
