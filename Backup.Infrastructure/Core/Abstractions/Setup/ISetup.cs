namespace Backup.Infrastructure.Core.Abstractions.Setup;

public interface ISetup
{
    public string? Id => null;
    public Task Setup();
}
