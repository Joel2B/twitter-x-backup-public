namespace Backup.Application.Dump;

public sealed class DumpContextGuardService : IDumpContextGuardService
{
    public string ResolveType(string? currentSession, string contextId, string? existingType)
    {
        if (string.IsNullOrWhiteSpace(currentSession))
            return contextId;

        if (!string.Equals(contextId, existingType, StringComparison.Ordinal))
            throw new Exception();

        return contextId;
    }
}
