using Backup.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.DependencyInjection;

public static class BackupInfrastructureSetupExtensions
{
    public static async Task RunBackupInfrastructureSetup(this IServiceProvider provider)
    {
        IEnumerable<ISetup> setups = provider.GetServices<ISetup>();

        foreach (ISetup setup in setups)
        {
            ILogger logger = provider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(setup.GetType());

            if (string.IsNullOrWhiteSpace(setup.Id))
            {
                logger.LogInformation("Setup: {SetupClass}", setup.GetType().Name);
            }
            else
            {
                logger.LogInformation(
                    "[{SetupId}] Setup: {SetupClass}",
                    setup.Id,
                    setup.GetType().Name
                );
            }

            await setup.Setup();
        }
    }
}
