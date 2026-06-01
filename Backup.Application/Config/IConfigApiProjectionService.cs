using Backup.Application.Config.Models;

namespace Backup.Application.Config;

public interface IConfigApiProjectionService
{
    IReadOnlyList<ConfigApiEntry> ToEntries(
        IReadOnlyDictionary<string, ConfigApiProjection> projections
    );
    IReadOnlyDictionary<string, ConfigApiProjection> ToProjections(
        IReadOnlyList<ConfigApiEntry> entries
    );
}
