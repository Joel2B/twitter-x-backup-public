using Backup.Infrastructure.Media.Abstractions.Data;
using Backup.Infrastructure.Media.IO;
using Backup.Infrastructure.Media.Models.Backup;
using Backup.Infrastructure.Models.Utils;
using Backup.Infrastructure.Utility.Abstractions.Services;

namespace Backup.Tests;

public sealed class MediaBackupIOComponentsTests
{
    [Fact]
    public void ZipEntryReader_Returns_Dictionary_By_FullName()
    {
        MediaBackupZipEntryReaderIOService sut = new();
        FakeZipWriter zip = new(
            [
                new ZipEntry
                {
                    FullName = "a.jpg",
                    FileSize = 10,
                    Crc32 = 1,
                    LastWriteTime = DateTimeOffset.UtcNow,
                },
            ]
        );

        Dictionary<string, ZipEntry> entries = sut.ReadEntriesByFullName(zip);

        Assert.Single(entries);
        Assert.True(entries.ContainsKey("a.jpg"));
    }

    [Fact]
    public async Task ChunkPersistence_Saves_Single_Chunk()
    {
        MediaBackupChunkPersistenceIOService sut = new();
        FakeBackupData data = new();
        Chunk chunk = new() { Id = 7, Data = [] };

        await sut.SaveChunk(data, chunk);

        Assert.Equal(1, data.SaveCalls);
        Assert.Single(data.LastSaved!);
        Assert.Equal(7, data.LastSaved![0].Id);
    }

    private sealed class FakeZipWriter(IEnumerable<ZipEntry> entries) : IZipWriter
    {
        private readonly List<ZipEntry> _entries = entries.ToList();

        public Task AddEntry(string entryName, Stream stream) => Task.CompletedTask;

        public bool RemoveEntry(string entryName, bool duplicate = false, int skip = 1) => true;

        public IEnumerable<ZipEntry> GetEntries() => _entries;

        public void Dispose() { }
    }

    private sealed class FakeBackupData : IMediaBackupData
    {
        public int SaveCalls { get; private set; }
        public List<Chunk>? LastSaved { get; private set; }

        public Task<BackupChunks?> GetBackup() => Task.FromResult<BackupChunks?>(null);

        public Task<List<Chunk>?> GetChunks(CancellationToken token = default) =>
            Task.FromResult<List<Chunk>?>(null);

        public Task<Stream?> GetChunk(Chunk chunk) => Task.FromResult<Stream?>(null);

        public Task<string?> GetHash(string path) => Task.FromResult<string?>(null);

        public Task Save(List<Chunk> chunks)
        {
            SaveCalls++;
            LastSaved = chunks;
            return Task.CompletedTask;
        }

        public Task SaveBackup(BackupChunks backup) => Task.CompletedTask;

        public Task DeleteChunk(Chunk chunk) => Task.CompletedTask;

        public Task<bool> Exists(string path) => Task.FromResult(false);

        public Task<Stream> Write(string path) => Task.FromResult<Stream>(new MemoryStream());
    }
}
