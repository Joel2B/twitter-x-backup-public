using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Backup.Infrastructure.Media.Maintenance;

public sealed class VideoVariantLooseCleanupManifestSplitService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public VideoVariantLooseCleanupSplitIndex Split(
        string sourcePath,
        string outputDirectory,
        int chunkSize = 100_000
    )
    {
        if (chunkSize is <= 0 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be from 1 to 100000.");

        string source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source))
            throw new FileNotFoundException("Source manifest not found.", source);

        string tempRoot = Path.Combine(Path.GetTempPath(), $"loose-manifest-split-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            using PathStore paths = new(tempRoot);
            SourceMetadata metadata = ValidateAndIndex(source, paths);
            ValidateApprovalHash(source, metadata.ApprovalHash);

            string output = Path.GetFullPath(outputDirectory);
            if (File.Exists(output))
                throw new IOException($"Output directory is a file: {output}");
            Directory.CreateDirectory(output);
            if (Directory.EnumerateFileSystemEntries(output).Any())
                throw new InvalidOperationException("Output directory must be empty.");

            List<VideoVariantLooseCleanupSplitChunk> chunks = [];
            List<SourceOperation> pending = new(chunkSize);
            foreach (SourceOperation operation in ReadOperations(source))
            {
                pending.Add(operation);
                if (pending.Count == chunkSize)
                    chunks.Add(WriteChunk(output, metadata, paths, pending, chunks.Count + 1));
            }
            if (pending.Count > 0)
                chunks.Add(WriteChunk(output, metadata, paths, pending, chunks.Count + 1));

            VideoVariantLooseCleanupSplitIndex index = new()
            {
                SourceManifest = source,
                SourceApprovalHash = metadata.ApprovalHash,
                ChunkSize = chunkSize,
                Total = chunks.Count,
                Operations = chunks.Sum(value => value.Operations),
                Bytes = chunks.Sum(value => value.Bytes),
                Paths = chunks.Sum(value => value.Paths),
                Chunks = chunks,
            };
            WriteAtomic(Path.Combine(output, "index.json"), index);
            return index;
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static SourceMetadata ValidateAndIndex(string source, PathStore paths)
    {
        SourceMetadata metadata = new();
        bool sawPaths = false;
        bool sawOperations = false;
        bool sawIssues = false;
        HashSet<string> properties = new(StringComparer.Ordinal);
        using StreamReader stream = new(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 64 * 1024);
        using JsonTextReader reader = new(stream) { DateParseHandling = DateParseHandling.None };
        Require(reader.Read() && reader.TokenType == JsonToken.StartObject, "Source manifest root must be an object.");
        while (reader.Read() && reader.TokenType != JsonToken.EndObject)
        {
            Require(reader.TokenType == JsonToken.PropertyName, "Invalid source manifest property.");
            string property = (string)reader.Value!;
            Require(properties.Add(property), $"Duplicate source manifest property: {property}");
            Require(reader.Read(), $"Missing value for {property}.");
            switch (property)
            {
                case "Version": metadata.Version = ReadInt32(reader, property); break;
                case "CreatedUtc": Skip(reader); break;
                case "DataConfigPath": metadata.DataConfigPath = ReadString(reader, property); break;
                case "DataConfigLength": metadata.DataConfigLength = ReadInt64(reader, property); break;
                case "DataConfigLastWriteUtcTicks": metadata.DataConfigLastWriteUtcTicks = ReadInt64(reader, property); break;
                case "MediaStoreId": metadata.MediaStoreId = ReadString(reader, property); break;
                case "CacheId": metadata.CacheId = ReadString(reader, property); break;
                case "CacheNamespace": metadata.CacheNamespace = ReadString(reader, property); break;
                case "DirectRoot": metadata.DirectRoot = ReadString(reader, property); break;
                case "CacheRowsScanned": metadata.CacheRowsScanned = ReadInt64(reader, property); break;
                case "VideoEntries": metadata.VideoEntries = ReadInt64(reader, property); break;
                case "FileParallelism": metadata.FileParallelism = ReadInt32(reader, property); break;
                case "SelectionLimit":
                    Require(reader.TokenType == JsonToken.Null, "SelectionLimit must be null.");
                    metadata.SelectionLimitWasNull = true;
                    break;
                case "PhysicalChecked": metadata.PhysicalChecked = ReadBoolean(reader, property); break;
                case "Paths":
                    Require(!sawOperations, "Paths must precede Operations.");
                    ReadPaths(reader, paths);
                    sawPaths = true;
                    break;
                case "Operations":
                    Require(sawPaths, "Paths must precede Operations.");
                    ReadOperationArray(reader, operation =>
                    {
                        ValidateOperation(operation, paths.Count);
                        paths.MarkTouched(operation.RemovePathIndex);
                        paths.MarkTouched(operation.KeepPathIndex);
                        metadata.OperationCount++;
                    });
                    sawOperations = true;
                    break;
                case "Issues":
                    Require(sawOperations, "Operations must precede Issues.");
                    ReadIssueArray(reader, issue =>
                    {
                        Require(issue.Code == "keep-set-invalid", "Source contains fatal cleanup issue.");
                        Require(!paths.AnyTouchedInGroup(issue.Path), "keep-set-invalid issue touches an operation path.");
                    });
                    sawIssues = true;
                    break;
                case "ApprovalHash": metadata.ApprovalHash = ReadString(reader, property); break;
                default: throw new InvalidOperationException($"Unknown source manifest property: {property}");
            }
        }
        Require(reader.TokenType == JsonToken.EndObject && !reader.Read(), "Invalid trailing JSON content.");
        string[] requiredProperties =
        [
            "Version", "CreatedUtc", "DataConfigPath", "DataConfigLength",
            "DataConfigLastWriteUtcTicks", "MediaStoreId", "CacheId", "CacheNamespace",
            "DirectRoot", "CacheRowsScanned", "VideoEntries", "FileParallelism",
            "SelectionLimit", "PhysicalChecked", "Paths", "Operations", "Issues", "ApprovalHash",
        ];
        Require(requiredProperties.All(properties.Contains), "Source manifest is missing required properties.");
        Require(metadata.Version == 1, "Loose cleanup manifest version must be 1.");
        Require(metadata.PhysicalChecked, "Source manifest must be physically checked.");
        Require(metadata.SelectionLimitWasNull, "SelectionLimit must be present and null.");
        Require(sawPaths && sawOperations && sawIssues, "Source manifest arrays are incomplete.");
        Require(metadata.OperationCount > 0, "Source manifest has no operations.");
        Require(Path.IsPathFullyQualified(metadata.DataConfigPath), "DataConfigPath must be absolute.");
        Require(Path.IsPathFullyQualified(metadata.DirectRoot), "DirectRoot must be absolute.");
        Require(metadata.ApprovalHash.Length == 64 && metadata.ApprovalHash.All(Uri.IsHexDigit), "ApprovalHash must be SHA-256 hex.");
        paths.Flush();
        return metadata;
    }

    private static IEnumerable<SourceOperation> ReadOperations(string source)
    {
        using StreamReader stream = new(source, Encoding.UTF8, true, 64 * 1024);
        using JsonTextReader reader = new(stream) { DateParseHandling = DateParseHandling.None };
        while (reader.Read())
        {
            if (reader.TokenType != JsonToken.PropertyName || !string.Equals((string?)reader.Value, "Operations", StringComparison.Ordinal))
                continue;
            Require(reader.Read(), "Missing Operations value.");
            Require(reader.TokenType == JsonToken.StartArray, "Operations must be an array.");
            Newtonsoft.Json.JsonSerializer serializer = Newtonsoft.Json.JsonSerializer.CreateDefault();
            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                Require(reader.TokenType == JsonToken.StartObject, "Operation must be an object.");
                SourceOperation? operation = JObject.Load(reader).ToObject<SourceOperation>(serializer);
                Require(operation is not null, "Invalid operation.");
                yield return operation!;
            }
            Require(reader.TokenType == JsonToken.EndArray, "Unterminated Operations array.");
            yield break;
        }
        throw new InvalidOperationException("Source manifest has no Operations array.");
    }

    private static void ReadPaths(JsonTextReader reader, PathStore paths)
    {
        Require(reader.TokenType == JsonToken.StartArray, "Paths must be an array.");
        string? previous = null;
        while (reader.Read() && reader.TokenType != JsonToken.EndArray)
        {
            string path = ReadString(reader, "Paths item");
            Require(previous is null || StringComparer.Ordinal.Compare(previous, path) < 0, "Path table must be unique and ordinal sorted.");
            paths.Add(path);
            previous = path;
        }
        Require(reader.TokenType == JsonToken.EndArray, "Unterminated Paths array.");
    }

    private static void ReadOperationArray(JsonTextReader reader, Action<SourceOperation> consume)
    {
        Require(reader.TokenType == JsonToken.StartArray, "Operations must be an array.");
        Newtonsoft.Json.JsonSerializer serializer = Newtonsoft.Json.JsonSerializer.CreateDefault();
        while (reader.Read() && reader.TokenType != JsonToken.EndArray)
        {
            Require(reader.TokenType == JsonToken.StartObject, "Operation must be an object.");
            JObject value = JObject.Load(reader);
            string[] required = ["RemovePathIndex", "KeepPathIndex", "PartitionId", "OriginalSize", "OriginalLastWriteUtcTicks", "DirectPresent", "DirectSize", "DirectLastWriteUtcTicks"];
            Require(required.All(name => value.Property(name, StringComparison.Ordinal) is not null), "Operation is missing required fields.");
            SourceOperation? operation = value.ToObject<SourceOperation>(serializer);
            Require(operation is not null, "Invalid operation.");
            consume(operation!);
        }
        Require(reader.TokenType == JsonToken.EndArray, "Unterminated Operations array.");
    }

    private static void ReadIssueArray(JsonTextReader reader, Action<VideoVariantLooseCleanupIssue> consume)
    {
        Require(reader.TokenType == JsonToken.StartArray, "Issues must be an array.");
        Newtonsoft.Json.JsonSerializer serializer = Newtonsoft.Json.JsonSerializer.CreateDefault();
        while (reader.Read() && reader.TokenType != JsonToken.EndArray)
        {
            Require(reader.TokenType == JsonToken.StartObject, "Issue must be an object.");
            VideoVariantLooseCleanupIssue? issue = JObject.Load(reader).ToObject<VideoVariantLooseCleanupIssue>(serializer);
            Require(issue is not null && issue.Path is not null && issue.Code is not null, "Invalid issue.");
            consume(issue!);
        }
        Require(reader.TokenType == JsonToken.EndArray, "Unterminated Issues array.");
    }

    private static VideoVariantLooseCleanupSplitChunk WriteChunk(
        string output,
        SourceMetadata metadata,
        PathStore sourcePaths,
        List<SourceOperation> sourceOperations,
        int number
    )
    {
        Dictionary<string, int> indices = new(StringComparer.Ordinal);
        List<string> paths = [];
        List<VideoVariantLooseCleanupOperation> operations = new(sourceOperations.Count);
        foreach (SourceOperation source in sourceOperations)
        {
            int remove = AddPath(sourcePaths.Get(source.RemovePathIndex));
            int keep = AddPath(sourcePaths.Get(source.KeepPathIndex));
            operations.Add(new()
            {
                RemovePathIndex = remove,
                KeepPathIndex = keep,
                PartitionId = source.PartitionId,
                KeepPartitionId = source.KeepPartitionId,
                OriginalSize = source.OriginalSize,
                OriginalLastWriteUtcTicks = source.OriginalLastWriteUtcTicks,
                DirectPresent = source.DirectPresent,
                DirectSize = source.DirectSize,
                DirectLastWriteUtcTicks = source.DirectLastWriteUtcTicks,
            });
        }

        int AddPath(string path)
        {
            if (indices.TryGetValue(path, out int index))
                return index;
            index = paths.Count;
            paths.Add(path);
            indices.Add(path, index);
            return index;
        }

        VideoVariantLooseCleanupManifest manifest = new()
        {
            Version = 1,
            CreatedUtc = DateTimeOffset.UtcNow,
            DataConfigPath = metadata.DataConfigPath,
            DataConfigLength = metadata.DataConfigLength,
            DataConfigLastWriteUtcTicks = metadata.DataConfigLastWriteUtcTicks,
            MediaStoreId = metadata.MediaStoreId,
            CacheId = metadata.CacheId,
            CacheNamespace = metadata.CacheNamespace,
            DirectRoot = metadata.DirectRoot,
            CacheRowsScanned = metadata.CacheRowsScanned,
            VideoEntries = metadata.VideoEntries,
            FileParallelism = 1,
            SelectionLimit = null,
            PhysicalChecked = true,
            Paths = paths,
            Operations = operations,
            Issues = [],
        };
        Require(
            manifest.Operations.All(value =>
                value.RemovePathIndex >= 0
                && value.RemovePathIndex < manifest.Paths.Count
                && value.KeepPathIndex >= 0
                && value.KeepPathIndex < manifest.Paths.Count
            ),
            $"Internal chunk path remapping failed for chunk {number}."
        );
        manifest.ApprovalHash = VideoVariantLooseCleanupPreviewService.CalculateApprovalHash(manifest);
        string fileName = $"chunk-{number:D6}.json";
        new VideoVariantLooseCleanupPreviewService().SaveManifest(manifest, Path.Combine(output, fileName + ".partial"));
        File.Move(Path.Combine(output, fileName + ".partial"), Path.Combine(output, fileName));

        long bytes = sourceOperations.Sum(value => checked(value.OriginalSize + (value.DirectPresent ? value.DirectSize : 0)));
        VideoVariantLooseCleanupSplitChunk result = new(fileName, manifest.ApprovalHash, operations.Count, bytes, paths.Count);
        sourceOperations.Clear();
        return result;
    }

    private static void ValidateApprovalHash(string source, string expected)
    {
        using FileStream stream = File.OpenRead(source);
        int tailLength = checked((int)Math.Min(stream.Length, 512));
        byte[] tail = new byte[tailLength];
        stream.Position = stream.Length - tailLength;
        stream.ReadExactly(tail);
        byte[] marker = Encoding.UTF8.GetBytes("\"ApprovalHash\": \"");
        int markerIndex = tail.AsSpan().LastIndexOf(marker);
        Require(markerIndex >= 0, "ApprovalHash is not in canonical WriteIndented position.");
        int hashIndex = markerIndex + marker.Length;
        Require(hashIndex + 64 < tail.Length && tail[hashIndex + 64] == (byte)'\"', "ApprovalHash has invalid canonical form.");
        Require(Encoding.ASCII.GetString(tail, hashIndex, 64).Equals(expected, StringComparison.Ordinal), "ApprovalHash JSON value is invalid.");

        long absoluteHashIndex = stream.Length - tailLength + hashIndex;
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        stream.Position = 0;
        byte[] buffer = new byte[1024 * 1024];
        long remaining = absoluteHashIndex;
        while (remaining > 0)
        {
            int read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            Require(read > 0, "Unexpected end of source manifest.");
            hash.AppendData(buffer, 0, read);
            remaining -= read;
        }
        stream.Position += 64;
        while (true)
        {
            int read = stream.Read(buffer);
            if (read == 0)
                break;
            hash.AppendData(buffer, 0, read);
        }
        string actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        Require(actual.Equals(expected, StringComparison.OrdinalIgnoreCase), "Loose cleanup manifest approval hash is invalid.");
    }

    private static void WriteAtomic<T>(string path, T value)
    {
        string partial = path + ".partial";
        try
        {
            using (FileStream stream = File.Create(partial))
                System.Text.Json.JsonSerializer.Serialize(stream, value, JsonOptions);
            File.Move(partial, path);
        }
        finally
        {
            if (File.Exists(partial))
                File.Delete(partial);
        }
    }

    private static void ValidateOperation(SourceOperation operation, int pathCount)
    {
        Require(operation.RemovePathIndex >= 0 && operation.RemovePathIndex < pathCount, "Operation remove path index is out of range.");
        Require(operation.KeepPathIndex >= 0 && operation.KeepPathIndex < pathCount, "Operation keep path index is out of range.");
        Require(operation.RemovePathIndex != operation.KeepPathIndex, "Operation remove and keep paths must differ.");
        Require(operation.OriginalSize >= 0 && operation.DirectSize >= 0, "Operation size must not be negative.");
    }

    private static string ReadString(JsonTextReader reader, string name)
    {
        Require(reader.TokenType == JsonToken.String, $"{name} must be a string.");
        return (string)reader.Value!;
    }

    private static int ReadInt32(JsonTextReader reader, string name)
    {
        long value = ReadInt64(reader, name);
        Require(value is >= int.MinValue and <= int.MaxValue, $"{name} is out of range.");
        return (int)value;
    }

    private static long ReadInt64(JsonTextReader reader, string name)
    {
        Require(reader.TokenType == JsonToken.Integer, $"{name} must be an integer.");
        return Convert.ToInt64(reader.Value);
    }

    private static bool ReadBoolean(JsonTextReader reader, string name)
    {
        Require(reader.TokenType == JsonToken.Boolean, $"{name} must be boolean.");
        return (bool)reader.Value!;
    }

    private static void Skip(JsonTextReader reader)
    {
        if (reader.TokenType is JsonToken.StartArray or JsonToken.StartObject)
            reader.Skip();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class SourceMetadata
    {
        public int Version;
        public string DataConfigPath = string.Empty;
        public long DataConfigLength;
        public long DataConfigLastWriteUtcTicks;
        public string MediaStoreId = string.Empty;
        public string CacheId = string.Empty;
        public string CacheNamespace = string.Empty;
        public string DirectRoot = string.Empty;
        public long CacheRowsScanned;
        public long VideoEntries;
        public int FileParallelism;
        public bool SelectionLimitWasNull;
        public bool PhysicalChecked;
        public long OperationCount;
        public string ApprovalHash = string.Empty;
    }

    private sealed class SourceOperation
    {
        public int RemovePathIndex { get; set; }
        public int KeepPathIndex { get; set; }
        public int PartitionId { get; set; }
        public int? KeepPartitionId { get; set; }
        public long OriginalSize { get; set; }
        public long OriginalLastWriteUtcTicks { get; set; }
        public bool DirectPresent { get; set; }
        public long DirectSize { get; set; }
        public long DirectLastWriteUtcTicks { get; set; }
    }

    private sealed class PathStore : IDisposable
    {
        private readonly FileStream _data;
        private readonly FileStream _index;
        private readonly FileStream _touched;
        public int Count { get; private set; }

        public PathStore(string root)
        {
            _data = File.Create(Path.Combine(root, "paths.data"));
            _index = File.Create(Path.Combine(root, "paths.index"));
            _touched = File.Create(Path.Combine(root, "paths.touched"));
        }

        public void Add(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            using BinaryWriter index = new(_index, Encoding.UTF8, leaveOpen: true);
            index.Write(_data.Position);
            index.Write(bytes.Length);
            _data.Write(bytes);
            _touched.WriteByte(0);
            Count++;
        }

        public void Flush()
        {
            _data.Flush();
            _index.Flush();
            _touched.Flush();
        }

        public string Get(int index)
        {
            Require(index >= 0 && index < Count, "Path index is out of range.");
            _index.Position = index * 12L;
            using BinaryReader offsets = new(_index, Encoding.UTF8, leaveOpen: true);
            long offset = offsets.ReadInt64();
            int length = offsets.ReadInt32();
            byte[] bytes = new byte[length];
            _data.Position = offset;
            _data.ReadExactly(bytes);
            return Encoding.UTF8.GetString(bytes);
        }

        public bool AnyTouchedInGroup(string group)
        {
            string prefix = group.EndsWith('/') ? group : group + "/";
            int low = 0;
            int high = Count;
            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                if (StringComparer.Ordinal.Compare(Get(middle), group) < 0)
                    low = middle + 1;
                else
                    high = middle;
            }
            for (int index = low; index < Count; index++)
            {
                string path = Get(index);
                if (!path.Equals(group, StringComparison.Ordinal) && !path.StartsWith(prefix, StringComparison.Ordinal))
                    break;
                if (IsTouched(index))
                    return true;
            }
            return false;
        }

        public void MarkTouched(int index)
        {
            _touched.Position = index;
            _touched.WriteByte(1);
        }

        public bool IsTouched(int index)
        {
            _touched.Position = index;
            return _touched.ReadByte() == 1;
        }

        public void Dispose()
        {
            _data.Dispose();
            _index.Dispose();
            _touched.Dispose();
        }
    }
}

public sealed record VideoVariantLooseCleanupSplitChunk(
    string Manifest,
    string ApprovalHash,
    int Operations,
    long Bytes,
    int Paths
);

public sealed record VideoVariantLooseCleanupSplitIndex
{
    public string SourceManifest { get; init; } = string.Empty;
    public string SourceApprovalHash { get; init; } = string.Empty;
    public int ChunkSize { get; init; }
    public int Total { get; init; }
    public long Operations { get; init; }
    public long Bytes { get; init; }
    public long Paths { get; init; }
    public List<VideoVariantLooseCleanupSplitChunk> Chunks { get; init; } = [];
}
