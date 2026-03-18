using Backup.App.Models.Media.Logging;

namespace Backup.App.Interfaces.Services.Media;

public interface IMediaLogger
{
    public void Error(Logs log, bool logger = true);
    public void Log(Logs log);
    public Task Save();
    public Task SaveErrors(List<Logs> logs);
    public Task<List<Logs>?> GetErrors();
    public Task<List<Logs>> GetMemoryErrors();
}
