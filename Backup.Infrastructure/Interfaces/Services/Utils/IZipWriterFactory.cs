namespace Backup.Infrastructure.Utility.Abstractions.Services;

public interface IZipWriterFactory
{
    public IZipWriter Create(Stream stream);
    public IZipWriter Open(Stream stream);
}
