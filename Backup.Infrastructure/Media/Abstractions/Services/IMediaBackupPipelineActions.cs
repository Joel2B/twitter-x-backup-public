namespace Backup.Infrastructure.Interfaces.Services.Media;

public interface IMediaBackupPipelineActions
{
    public bool ShouldStop { get; }
    public Task CalculateAsync();
    public Task CalculateDirectAsync();
    public Task ApplyDirectAsync();
    public Task ApplyAsync();
    public Task CheckDuplicatesAsync();
    public Task SetFileSizesAsync();
    public Task CheckIntegrityAsync();
    public Task FixIntegrityAsync();
}
