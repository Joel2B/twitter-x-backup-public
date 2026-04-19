using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Backup.App.Utils;

public static class PostHash
{
    public static string Compute(Models.Post.Post post)
    {
        string json = JsonConvert.SerializeObject(post);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        byte[] hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }
}
