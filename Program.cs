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

using ServiceProvider provider = services.BuildServiceProvider();
using IServiceScope scope = provider.CreateScope();
await scope.ServiceProvider.RunSetup();

Backup.App.App app = scope.ServiceProvider.GetRequiredService<Backup.App.App>();
await app.Backup();
