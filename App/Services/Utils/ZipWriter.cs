using System.IO.Compression;
using Backup.App.Interfaces.Services.UtilsService;
using Backup.App.Models.Utils;

namespace Backup.App.Services.UtilsService;

public class ZipWriter : IZipWriter
{
    private bool _disposed = false;

    private readonly ZipArchive _zip;
    private readonly Stream _stream;

    public ZipWriter(Stream source, ZipArchiveMode mode)
    {
        _stream = source;
        _zip = new ZipArchive(_stream, mode, leaveOpen: true);
    }

    public async Task AddEntry(string entryName, Stream source)
    {
        if (source.CanSeek && source.Position != 0)
            source.Position = 0;

        ZipArchiveEntry entry = _zip.CreateEntry(entryName, CompressionLevel.NoCompression);
        await using Stream entryStream = entry.Open();

        await source.CopyToAsync(entryStream);
    }

    public bool RemoveEntry(string entryName, bool duplicate = false, int skip = 1)
    {
        try
        {
            List<ZipArchiveEntry> entries = [];

            if (duplicate)
            {
                List<ZipArchiveEntry> duplicates = _zip
                    .Entries.Where(e => !string.IsNullOrEmpty(e.Name))
                    .Where(o => o.FullName.Contains(entryName))
                    .Skip(skip)
                    .ToList();

                entries.AddRange(duplicates);
            }
            else
            {
                ZipArchiveEntry? entry = _zip.GetEntry(entryName);

                if (entry is not null)
                    entries.Add(entry);
            }

            if (entries.Count == 0)
                return false;

            foreach (ZipArchiveEntry entry in entries)
                entry.Delete();

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public IEnumerable<ZipEntry> GetEntries() =>
        _zip
            .Entries.Where(e => !string.IsNullOrEmpty(e.Name))
            .Select(e => new ZipEntry()
            {
                FullName = e.FullName,
                FileSize = e.Length,
                LastWriteTime = e.LastWriteTime,
                Crc32 = e.Crc32,
            })
            .ToList();

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _zip.Dispose();
            _stream.Dispose();
        }

        _disposed = true;
    }
}
