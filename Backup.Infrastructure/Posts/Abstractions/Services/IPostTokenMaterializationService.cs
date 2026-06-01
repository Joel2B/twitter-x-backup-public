using Backup.Application.Posts.Models;
using Newtonsoft.Json.Linq;

namespace Backup.Infrastructure.Posts.Abstractions.Services;

public interface IPostTokenMaterializationService
{
    PostTokenMaterializationBatchResult<T> MaterializeMany<T>(IEnumerable<JObject> tokens)
        where T : class;

    T? Materialize<T>(JToken token)
        where T : class;
}
