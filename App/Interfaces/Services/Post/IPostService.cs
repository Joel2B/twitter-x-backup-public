namespace Backup.App.Interfaces.Services.Post;

public interface IPostService
{
    public Task Download(Models.Config.FetchContext fetchContext);
}
