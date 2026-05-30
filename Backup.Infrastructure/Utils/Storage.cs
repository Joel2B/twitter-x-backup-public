using Backup.Application.IO;

namespace Backup.Infrastructure.Utils;

public class UtilsStorage
{
    public static string FormatBytes(long bytes) => ByteSizeFormattingPolicy.FormatBytes(bytes);
}
