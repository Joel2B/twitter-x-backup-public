using Backup.App.Interfaces.Services;
using Backup.App.Services.Bulk;
using Backup.App.Services.Media;
using Backup.App.Services.Post;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class ServicesCollectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IService, PostService>();
        services.AddScoped<IService, MediaService>();
        services.AddScoped<IService, BulkService>();

        return services;
    }
}
