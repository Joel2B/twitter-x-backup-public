using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class AutoMapperCollectionExtensions
{
    public static IServiceCollection AddMapper(this IServiceCollection services)
    {
        AutoMapper.IMapper mapper = Mapper.Mapper.Setup();
        services.AddSingleton(mapper);

        return services;
    }
}
