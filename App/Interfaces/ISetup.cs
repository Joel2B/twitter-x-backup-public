namespace Backup.App.Interfaces;

public interface ISetup
{
    public string? Id => null;
    public Task Setup();
}
