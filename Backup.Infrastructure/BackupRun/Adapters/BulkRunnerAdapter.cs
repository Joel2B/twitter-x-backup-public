using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Interfaces.Services.Media;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.BackupRun.Adapters;

public class BulkRunnerAdapter(
    AppConfig config,
    IEnumerable<IBulkService> bulkServices,
    ILogger<BulkRunnerAdapter> logger
) : IBulkRunner
{
    private readonly AppConfig _config = config;
    private readonly IEnumerable<IBulkService> _bulkServices = bulkServices;
    private readonly ILogger<BulkRunnerAdapter> _logger = logger;

    public async Task Run(string userId)
    {
        UsersContext? userContext = _config.UsersContext.FirstOrDefault(context =>
            context.UserId == userId
        );

        if (userContext is null)
            return;

        foreach (IBulkService service in _bulkServices)
        {
            using (_logger.LogTimer($"bulk service: {service.GetType().Name}"))
                await service.Download(userContext);
        }
    }
}
