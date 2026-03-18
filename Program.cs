using Backup.App.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

ServiceCollection services = new();

await services.AddCore();
services.AddTasks();
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
await provider.RunSetup();

Backup.App.App app = provider.GetRequiredService<Backup.App.App>();
await app.Backup();
