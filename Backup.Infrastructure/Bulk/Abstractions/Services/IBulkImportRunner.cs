using Backup.Infrastructure.Models.Config.Api;

namespace Backup.Infrastructure.Interfaces.Services.Bulk;

public interface IBulkImportRunner
{
    Task Run(IReadOnlyDictionary<string, ApiConfig> api, CancellationToken cancellationToken);
}

