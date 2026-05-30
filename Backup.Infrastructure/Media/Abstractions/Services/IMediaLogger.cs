using Backup.Infrastructure.Models.Media.Logging;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaLogger
{
    public Task<List<Logs>?> GetErrors();
    public Task RemoveErrors(List<Logs> logs);
    public void Error(Logs log);
    public void Log(Logs log);
    public Task Save();
}
