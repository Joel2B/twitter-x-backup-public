using Backup.Application.Diagnostics.Models;

namespace Backup.Application.Diagnostics;

public static class TextDiffPolicy
{
    public static TextDiffResult Diff(string left, string right)
    {
        string[] leftLines = left.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        string[] rightLines = right.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        List<string> leftOnly = [.. leftLines.Except(rightLines)];
        List<string> rightOnly = [.. rightLines.Except(leftLines)];

        return new() { LeftOnlyLines = leftOnly, RightOnlyLines = rightOnly };
    }
}
