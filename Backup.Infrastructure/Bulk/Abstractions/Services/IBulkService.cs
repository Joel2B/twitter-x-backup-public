using Backup.Infrastructure.Models.Config.Api;

namespace Backup.Infrastructure.Bulk.Abstractions.Services;

public interface IBulkService
{
    public Task Download(UsersContext context);
}
