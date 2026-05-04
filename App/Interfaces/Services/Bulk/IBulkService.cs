using Backup.App.Models.Config.Api;

namespace Backup.App.Interfaces.Services.Media;

public interface IBulkService
{
    public Task Download(UsersContext context);
}
