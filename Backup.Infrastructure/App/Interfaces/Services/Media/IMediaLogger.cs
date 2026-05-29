using Backup.App.Models.Media.Logging;

namespace Backup.App.Interfaces.Services.Media;

public interface IMediaLogger
{
    public Task<List<Logs>?> GetErrors();
    public Task RemoveErrors(List<Logs> logs);
    public void Error(Logs log);
    public void Log(Logs log);
    public Task Save();
}
