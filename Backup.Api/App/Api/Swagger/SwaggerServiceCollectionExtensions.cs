using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Backup.Api.Swagger;

public static class SwaggerServiceCollectionExtensions
{
    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(ConfigureSwagger);
        return services;
    }

    private static void ConfigureSwagger(SwaggerGenOptions options)
    {
        options.SwaggerDoc(
            "v1",
            new OpenApiInfo
            {
                Title = "Twitter X Backup API",
                Version = "v1",
                Description =
                    "REST endpoints for ingesting X posts (raw GraphQL or processed post models).",
            }
        );

        options.SupportNonNullableReferenceTypes();
        options.OperationFilter<PostsApiExamplesOperationFilter>();

        string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

        if (File.Exists(xmlPath))
            options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
}

