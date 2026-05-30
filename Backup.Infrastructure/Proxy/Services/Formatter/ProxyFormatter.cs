using Backup.Infrastructure.Interfaces.Proxy;

namespace Backup.Infrastructure.Services.Proxy.Formatter;

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
