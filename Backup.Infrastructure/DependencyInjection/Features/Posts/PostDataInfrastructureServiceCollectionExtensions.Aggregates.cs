using Backup.Infrastructure.Posts.Data;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Adapters;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Features.Posts;

public static partial class PostDataInfrastructureServiceCollectionExtensions
{
    private static IServiceCollection RegisterPostDataAggregates(this IServiceCollection services)
    {
        services.AddScoped<IPostData, PostDataMultiStore>();
        services.AddScoped(sp => (PostDataMultiStore)sp.GetRequiredService<IPostData>());
        services.AddScoped<IPostStoreParityVerifier>(sp =>
            (PostDataMultiStore)sp.GetRequiredService<IPostData>()
        );
        services.AddScoped<IPostDomainData>(sp =>
        {
            IPostData postData = sp.GetRequiredService<IPostData>();
            return postData as IPostDomainData ?? new PostDataDomainAdapter(postData);
        });
        return services;
    }
}
