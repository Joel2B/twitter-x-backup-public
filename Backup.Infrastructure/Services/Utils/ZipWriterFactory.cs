using System.IO.Compression;
using Backup.Infrastructure.Utility.Abstractions.Services;

namespace Backup.Infrastructure.Services.UtilsService;

public class ZipWriterFactory : IZipWriterFactory
{
    public IZipWriter Create(Stream stream) => new ZipWriter(stream, ZipArchiveMode.Update);

    public IZipWriter Open(Stream stream) => new ZipWriter(stream, ZipArchiveMode.Read);
}
