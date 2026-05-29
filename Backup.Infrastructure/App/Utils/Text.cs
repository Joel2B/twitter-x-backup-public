using Backup.App.Models.Utils;

namespace Backup.App.Utils;

public class Text
{
    public static Diff Diff(string json1, string json2)
    {
        string[] lines1 = json1.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        string[] lines2 = json2.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        List<string> diff1 = [.. lines1.Except(lines2)];
        List<string> diff2 = [.. lines2.Except(lines1)];

        return new() { Diff1 = diff1, Diff2 = diff2 };
    }
}
