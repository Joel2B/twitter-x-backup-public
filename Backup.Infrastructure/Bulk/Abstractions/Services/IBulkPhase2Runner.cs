using Backup.Infrastructure.Models.Config.Api;

namespace Backup.Infrastructure.Interfaces.Services.Bulk;

public interface IBulkPhase2Runner
{
    Task Run(IReadOnlyDictionary<string, ApiConfig> api, CancellationToken cancellationToken);
}

