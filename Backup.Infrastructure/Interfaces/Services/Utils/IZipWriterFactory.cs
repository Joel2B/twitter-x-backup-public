namespace Backup.Infrastructure.Interfaces.Services.UtilsService;

public interface IZipWriterFactory
{
    public IZipWriter Create(Stream stream);
    public IZipWriter Open(Stream stream);
}

