using Backup.Infrastructure.Models.Config.Api;

namespace Backup.Infrastructure.Bulk.Abstractions.Services;

public interface IBulkPhase1Runner
{
    Task Run(IReadOnlyDictionary<string, ApiConfig> api, CancellationToken cancellationToken);
}
