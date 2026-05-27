using Microsoft.AspNetCore.Builder;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace Backup.App.Api.Swagger;

public static class SwaggerApplicationBuilderExtensions
{
    public static WebApplication UseApiSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Twitter X Backup API v1");
            options.DocumentTitle = "Twitter X Backup API";
            options.DocExpansion(DocExpansion.List);
            options.DisplayRequestDuration();
            options.EnableDeepLinking();
            options.DefaultModelsExpandDepth(2);
        });

        return app;
    }
}
