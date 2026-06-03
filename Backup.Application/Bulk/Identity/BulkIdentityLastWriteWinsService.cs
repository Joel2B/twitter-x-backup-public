namespace Backup.Application.Bulk;

public sealed class BulkIdentityLastWriteWinsService : IBulkIdentityLastWriteWinsService
{
    public IReadOnlyDictionary<string, int> BuildLastIndexByKey(IEnumerable<string> keys)
    {
        Dictionary<string, int> result = new(StringComparer.Ordinal);
        int index = 0;

        foreach (string key in keys)
            result[key] = index++;

        return result;
    }
}
