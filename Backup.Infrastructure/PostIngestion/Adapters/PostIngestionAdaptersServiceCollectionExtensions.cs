using Backup.Application.PostIngestion.Ports;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Posts.Adapters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Backup.Infrastructure.PostIngestion.Adapters;

public static class PostIngestionAdaptersServiceCollectionExtensions
{
    public static IServiceCollection AddPostIngestionAdapters(this IServiceCollection services)
    {
        services.TryAddScoped<IPostDomainData>(sp =>
        {
            IPostData postData = sp.GetRequiredService<IPostData>();
            return postData as IPostDomainData ?? new PostDataDomainAdapter(postData);
        });
        services.AddScoped<IRawPostParser, RawPostParserAdapter>();
        services.AddScoped<IPostStoreWriter, PostStoreWriterAdapter>();
        return services;
    }
}
