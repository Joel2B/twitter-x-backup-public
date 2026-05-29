namespace Backup.App.Interfaces.Services.UtilsService;

public interface IZipWriterFactory
{
    public IZipWriter Create(Stream stream);
    public IZipWriter Open(Stream stream);
}
