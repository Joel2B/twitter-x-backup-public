using System.Globalization;

namespace Backup.Application.Media;

public sealed class MediaLogFilePolicyService : IMediaLogFilePolicyService
{
    private const string DateFormat = "yyyy.MM.dd-HH.mm.ss";

    public string CreateFileName(DateTime now) => $"{now.ToString(DateFormat)}.json";

    public string? SelectLatestFilePath(IEnumerable<string> paths)
    {
        return paths
            .Select(path => new
            {
                Path = path,
                Date = TryParse(Path.GetFileNameWithoutExtension(path)),
            })
            .Where(item => item.Date is not null)
            .OrderByDescending(item => item.Date)
            .Select(item => item.Path)
            .FirstOrDefault();
    }

    private static DateTime? TryParse(string value)
    {
        return DateTime.TryParseExact(
            value,
            DateFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime date
        )
            ? date
            : null;
    }
}
