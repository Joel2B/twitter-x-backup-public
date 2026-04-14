namespace Backup.App.Models.Config;

public sealed class FetchContext
{
    public required Source Source { get; init; }

    public string UserId =>
        Source.Request.Query.Variables["userId"]?.ToString() ?? throw new Exception("userId");
}
