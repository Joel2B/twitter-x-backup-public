using Backup.Infrastructure.Models.Config.Api;

namespace Backup.Infrastructure.Interfaces.Services.Media;

public interface IBulkService
{
    public Task Download(UsersContext context);
}
