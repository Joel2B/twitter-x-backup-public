using System.Text;

namespace Backup.Application.Partition;

public sealed class PartitionPathProbeService : IPartitionPathProbeService
{
    public string? Probe(string path)
    {
        try
        {
            using FileStream fs = new(
                path,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 16 * 1024,
                options: FileOptions.WriteThrough
            );

            byte[] payload = Encoding.UTF8.GetBytes("ok");
            fs.Write(payload, 0, payload.Length);
            fs.Flush(true);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        return null;
    }
}
