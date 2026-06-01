using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public interface IPostMediaInputsCompositionService
{
    IReadOnlyList<MediaInput> Compose(IReadOnlyCollection<Post> posts);
}
