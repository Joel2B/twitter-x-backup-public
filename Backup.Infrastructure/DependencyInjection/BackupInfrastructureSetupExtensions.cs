using Backup.App.Extensions;

namespace Backup.Infrastructure.DependencyInjection;

public static class BackupInfrastructureSetupExtensions
{
    public static Task RunBackupInfrastructureSetup(this IServiceProvider provider) =>
        provider.RunSetup();
}
