using Backup.Infrastructure.Proxy.Abstractions.Core;

namespace Backup.Infrastructure.Proxy.Services.Formatter;

public class ProxyFormatter
{
    public static IProxyFormatter Create(string format)
    {
        return format.ToLower() switch
        {
            "ipport" => new ProxyFormatterSimple(),
            _ => throw new NotSupportedException($"Tipo no soportado: {format}"),
        };
    }
}
