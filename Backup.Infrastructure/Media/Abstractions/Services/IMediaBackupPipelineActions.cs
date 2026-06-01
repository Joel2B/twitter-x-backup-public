namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaBackupPipelineActions
{
    public bool ShouldStop { get; }
    public Task CalculateAsync(CancellationToken cancellationToken = default);
    public Task CalculateDirectAsync(CancellationToken cancellationToken = default);
    public Task ApplyDirectAsync(CancellationToken cancellationToken = default);
    public Task ApplyAsync(CancellationToken cancellationToken = default);
    public Task CheckDuplicatesAsync(CancellationToken cancellationToken = default);
    public Task SetFileSizesAsync(CancellationToken cancellationToken = default);
    public Task CheckIntegrityAsync(CancellationToken cancellationToken = default);
    public Task FixIntegrityAsync(CancellationToken cancellationToken = default);
}
