namespace Backup.Application.Config;

public sealed class ConfigDeserializationGuardService
{
    public T RequireConfig<T>(T? value, string fileName)
    {
        if (value is not null)
            return value;

        throw new Exception($"error deserializing config file '{fileName}'");
    }
}
