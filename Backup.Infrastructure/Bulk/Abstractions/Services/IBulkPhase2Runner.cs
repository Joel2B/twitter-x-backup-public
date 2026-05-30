using Backup.Infrastructure.Models.Config.Api;

namespace Backup.Infrastructure.Bulk.Abstractions.Services;

public interface IBulkPhase2Runner
{
    Task Run(IReadOnlyDictionary<string, ApiConfig> api, CancellationToken cancellationToken);
}
