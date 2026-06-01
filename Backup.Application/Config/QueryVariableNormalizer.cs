using System.Globalization;

namespace Backup.Application.Config;

public static class QueryVariableNormalizer
{
    public static void Normalize(Dictionary<string, object?> variables)
    {
        List<string> keys = [.. variables.Keys];

        foreach (string key in keys)
            variables[key] = Coerce(variables[key]);
    }

    public static object? Coerce(object? value)
    {
        if (value is not string text)
            return value;

        if (bool.TryParse(text, out bool boolValue))
            return boolValue;

        if (
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue)
        )
            return intValue;

        if (
            long.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long longValue
            )
        )
            return longValue;

        if (
            double.TryParse(
                text,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out double doubleValue
            )
        )
            return doubleValue;

        return text;
    }
}
