using Newtonsoft.Json.Linq;
using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public interface IPostTokenMaterializationService
{
    PostTokenMaterializationBatchResult<T> MaterializeMany<T>(IEnumerable<JObject> tokens)
        where T : class;

    T? Materialize<T>(JToken token)
        where T : class;
}
