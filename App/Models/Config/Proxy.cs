namespace Backup.App.Models.Config.Proxy;

public class Proxy
{
    public required bool Enabled { get; set; }
    public required bool Check { get; set; }
    public required List<int> Partitions { get; set; }
    public required Data Data { get; set; }
    public required Threshold Threshold { get; set; }
    public required List<Provider> Providers { get; set; }
}

public class Threshold
{
    public int ErrorsToInactive { get; set; }
    public int ErrorsToStop { get; set; }
}

public class Data : Downloads.Path
{
    public required Downloads.Path Proxy { get; set; }
}

public class Provider
{
    public required string Type { get; set; }
    public required string Format { get; set; }
    public required List<Resource> Resources { get; set; }
}

public class Resource
{
    public required string Type { get; set; }
    public required string Value { get; set; }
}
