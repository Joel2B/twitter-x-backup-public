using Backup.Application.Posts.Models;
using Newtonsoft.Json.Linq;

namespace Backup.Application.Posts;

public sealed class PostTokenMaterializationService : IPostTokenMaterializationService
{
    public PostTokenMaterializationBatchResult<T> MaterializeMany<T>(IEnumerable<JObject> tokens)
        where T : class
    {
        List<T> items = [];
        List<string> errors = [];
        int index = 0;

        foreach (JObject token in tokens)
        {
            T? value = token.ToObject<T>();

            if (value is null)
                errors.Add($"token[{index}] materialization returned null for {typeof(T).Name}");
            else
                items.Add(value);

            index++;
        }

        return new PostTokenMaterializationBatchResult<T> { Items = items, Errors = errors };
    }

    public T? Materialize<T>(JToken token)
        where T : class => token.ToObject<T>();
}
