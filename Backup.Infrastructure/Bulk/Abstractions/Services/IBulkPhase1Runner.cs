using Backup.Infrastructure.Models.Config.Api;

namespace Backup.Infrastructure.Interfaces.Services.Bulk;

public interface IBulkPhase1Runner
{
    Task Run(IReadOnlyDictionary<string, ApiConfig> api, CancellationToken cancellationToken);
}

