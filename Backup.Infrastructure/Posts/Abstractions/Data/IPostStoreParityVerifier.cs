namespace Backup.Infrastructure.Posts.Abstractions.Data;

public interface IPostStoreParityVerifier
{
    Task VerifyStoreCounts();
}
