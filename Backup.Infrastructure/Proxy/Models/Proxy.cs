namespace Backup.Infrastructure.Models.Proxy;

public class ProxyDataConfig
{
    public required string Ip { get; set; }
    public required string Port { get; set; }
    public required string Protocol { get; set; }

    public override bool Equals(object? obj)
    {
        if (obj is not ProxyDataConfig proxy)
            return false;

        return Ip == proxy.Ip && Port == proxy.Port && Protocol == proxy.Protocol;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Ip, Port, Protocol);
    }

    public override string ToString() => $"{Protocol}://{Ip}:{Port}";
}

