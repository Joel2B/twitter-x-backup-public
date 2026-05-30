namespace Backup.Infrastructure.Interfaces;

public interface ISetup
{
    public string? Id => null;
    public Task Setup();
}
