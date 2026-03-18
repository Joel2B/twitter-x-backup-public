using Backup.App.Interfaces;
using Backup.App.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class TasksCollectionExtensions
{
    public static IServiceCollection AddTasks(this IServiceCollection services)
    {
        services.AddSingleton<LocalPostDataPrune>();
        services.AddSingleton<ISetup>(sp => sp.GetRequiredService<LocalPostDataPrune>());

        return services;
    }
}
