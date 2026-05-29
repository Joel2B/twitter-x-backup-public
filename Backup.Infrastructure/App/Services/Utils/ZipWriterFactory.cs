using System.IO.Compression;
using Backup.App.Interfaces.Services.UtilsService;

namespace Backup.App.Services.UtilsService;

public class ZipWriterFactory : IZipWriterFactory
{
    public IZipWriter Create(Stream stream) => new ZipWriter(stream, ZipArchiveMode.Update);

    public IZipWriter Open(Stream stream) => new ZipWriter(stream, ZipArchiveMode.Read);
}
