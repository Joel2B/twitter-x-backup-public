using Backup.Infrastructure.Models.Config.Data;
using Microsoft.EntityFrameworkCore;

namespace Backup.Infrastructure.Posts.Data.Postgres;

public partial class PostgresPostData
{
    private async Task EnsureSchema()
    {
        string connectionString = GetConnectionString();
        await using PostsDbContext db = CreateDbContext(connectionString);
        await db.Database.EnsureCreatedAsync();
    }

    private string GetConnectionString()
    {
        string? connectionString = _config.ConnectionString;

        if (!string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        throw new InvalidOperationException(
            $"Post postgres connection string is not configured for store '{Id ?? _config.Id ?? "unknown"}'."
        );
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    private static PostsDbContext CreateDbContext(string connectionString) => new(connectionString);
}
