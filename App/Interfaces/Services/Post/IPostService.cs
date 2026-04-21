namespace Backup.App.Interfaces.Services.Post;

public interface IPostService
{
    public Task Recover(Models.Config.FetchContext fetchContext);
    public Task Download(Models.Config.FetchContext fetchContext);
}
