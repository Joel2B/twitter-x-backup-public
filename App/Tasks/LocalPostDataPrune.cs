using Backup.App.Interfaces;

namespace Backup.App.Tasks;

public class File
{
    public required string Path { get; set; }
    public DateTime? Date { get; set; }
}

class LocalPostDataPrune() : ISetup
{
    public Task Setup()
    {
        return Task.CompletedTask;
    }
}
