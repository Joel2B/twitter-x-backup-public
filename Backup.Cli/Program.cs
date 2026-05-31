using Backup.Configuration;
using Backup.Infrastructure.DependencyInjection.Composition;
using Backup.Infrastructure.DependencyInjection.Runtime;
using Backup.Infrastructure.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

ServiceCollection services = new();
ConfigurationManager configuration = new();
configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
configuration.AddEnvironmentVariables();

services.AddBackupConfiguration(configuration);
services.AddBackupCliInfrastructure();

Console.Error.WriteLine("[startup] building service provider");
await using ServiceProvider provider = services.BuildServiceProvider();

Console.Error.WriteLine("[startup] creating scope");
await using AsyncServiceScope scope = provider.CreateAsyncScope();

Console.Error.WriteLine("[startup] running setup");
await scope.ServiceProvider.RunBackupInfrastructureSetup();

Console.Error.WriteLine("[startup] resolving app");
IBackupCliRunner cliRunner = scope.ServiceProvider.GetRequiredService<IBackupCliRunner>();

Console.Error.WriteLine("[startup] running backup");
await cliRunner.RunBackup();
