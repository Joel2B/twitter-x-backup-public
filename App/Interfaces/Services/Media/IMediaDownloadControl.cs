namespace Backup.App.Interfaces.Services.Media;

public interface IMediaDownloadControl
{
    public CancellationToken Token { get; set; }
    public void Check();
    public void Cancel();
}
