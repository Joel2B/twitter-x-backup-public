using Backup.Application.Diagnostics;
using Backup.Infrastructure.Models.Utils;

namespace Backup.Infrastructure.Utils;

public class Text
{
    public static Diff Diff(string json1, string json2)
    {
        Backup.Application.Diagnostics.Models.TextDiffResult diff = TextDiffPolicy.Diff(
            json1,
            json2
        );

        return new() { Diff1 = diff.LeftOnlyLines, Diff2 = diff.RightOnlyLines };
    }
}
