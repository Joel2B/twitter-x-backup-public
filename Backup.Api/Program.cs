using Backup.Api.Errors;
using Backup.Api.Services;
using Backup.Api.Swagger;
using Backup.Infrastructure.DependencyInjection.Composition;
using Backup.Infrastructure.DependencyInjection.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

Console.Error.WriteLine("[startup] creating web application builder");
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

Console.Error.WriteLine("[startup] registering services");
builder.Services.AddRuntimeConfiguration(builder.Configuration);
builder.Services.AddBackupApiInfrastructure();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddPostIngestionApi();
builder.Services.AddApiSwagger();

Console.Error.WriteLine("[startup] building web application");
WebApplication app = builder.Build();

Console.Error.WriteLine("[startup] creating scope");
await using AsyncServiceScope scope = app.Services.CreateAsyncScope();

Console.Error.WriteLine("[startup] running setup");
await scope.ServiceProvider.RunBackupInfrastructureSetup();

Console.Error.WriteLine("[startup] configuring middleware and routes");
app.UseExceptionHandler();
app.UseApiSwagger();
app.MapControllers();

Console.Error.WriteLine("[startup] running api");
await app.RunAsync();
