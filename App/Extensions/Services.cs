using Backup.App.Interfaces.Services.Media;
using Backup.App.Interfaces.Services.Post;
using Backup.App.Services.Bulk;
using Backup.App.Services.Media;
using Backup.App.Services.Post;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class ServicesCollectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IBulkService, BulkService>();

        return services;
    }
}
