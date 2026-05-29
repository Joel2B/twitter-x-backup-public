using Backup.App.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backup.App.Extensions;

public static class SetupExecutionExtensions
{
    public static async Task RunSetup(this IServiceProvider provider)
    {
        IEnumerable<ISetup> setups = provider.GetServices<ISetup>();

        foreach (ISetup setup in setups)
        {
            ILogger logger = provider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(setup.GetType());

            logger.LogInformation(setup.Id, "Setup: {class}", setup.GetType().Name);
            await setup.Setup();
        }
    }
}
