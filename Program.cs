using Backup.App;
using Backup.App.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

ServiceCollection services = new();

services.AddCore();
services.AddSerilog();

services.AddPostData();
services.AddDumpData();
services.AddBulkData();
services.AddMediaData();

services.AddUtils();
services.AddPost();
services.AddMedia();
services.AddMediaBackup();
services.AddServices();

services.AddSetup();
services.AddApp();

Console.Error.WriteLine("[startup] building service provider");
await using ServiceProvider provider = services.BuildServiceProvider();

Console.Error.WriteLine("[startup] creating scope");
await using AsyncServiceScope scope = provider.CreateAsyncScope();

Console.Error.WriteLine("[startup] running setup");
await scope.ServiceProvider.RunSetup();

Console.Error.WriteLine("[startup] resolving app");
App app = scope.ServiceProvider.GetRequiredService<App>();

Console.Error.WriteLine("[startup] running backup");
await app.Backup();
