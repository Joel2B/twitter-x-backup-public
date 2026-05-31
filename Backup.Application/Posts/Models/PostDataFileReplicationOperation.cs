namespace Backup.Application.Posts.Models;

public sealed class PostDataFileReplicationOperation
{
    public string SourcePath { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;
}
