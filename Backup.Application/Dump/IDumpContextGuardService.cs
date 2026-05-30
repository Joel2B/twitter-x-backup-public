namespace Backup.Application.Dump;

public interface IDumpContextGuardService
{
    string ResolveType(string? currentSession, string contextId, string? existingType);
}
