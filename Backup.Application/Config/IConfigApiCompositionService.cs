using Backup.Application.Config.Models;

namespace Backup.Application.Config;

public interface IConfigApiCompositionService
{
    IReadOnlyDictionary<string, ConfigApiProjection> NormalizeApi(
        IReadOnlyDictionary<string, ConfigApiProjection> projections
    );
    IReadOnlyDictionary<string, ConfigApiProjection> ApplyFetchToApi(
        IReadOnlyDictionary<string, ConfigApiProjection> projections,
        IReadOnlyList<ConfigFetchEntry> fetchEntries
    );
}
