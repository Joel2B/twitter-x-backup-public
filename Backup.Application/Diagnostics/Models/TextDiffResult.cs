namespace Backup.Application.Diagnostics.Models;

public sealed class TextDiffResult
{
    public required List<string> LeftOnlyLines { get; init; }
    public required List<string> RightOnlyLines { get; init; }
}
