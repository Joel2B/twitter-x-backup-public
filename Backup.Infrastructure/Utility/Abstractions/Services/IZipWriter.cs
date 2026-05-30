using Backup.Infrastructure.Models.Utils;

namespace Backup.Infrastructure.Utility.Abstractions.Services;

public interface IZipWriter : IDisposable
{
    public Task AddEntry(string entryName, Stream stream);
    public bool RemoveEntry(string entryName, bool duplicate = false, int skip = 1);
    public IEnumerable<ZipEntry> GetEntries();
}
