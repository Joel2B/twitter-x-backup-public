using System.Text;
using Microsoft.Extensions.Configuration;

namespace Backup.Infrastructure.DependencyInjection.Runtime;

internal static class RuntimeConfigurationDirectoryResolver
{
    public static string Resolve(
        RuntimeConfigurationOptions options,
        IConfiguration? configuration = null
    )
    {
        string? fromConfiguration = configuration?[RuntimeConfigurationOptions.ConfigDirectoryConfigurationKey];
        string? fromEnvironment = Environment.GetEnvironmentVariable(
            RuntimeConfigurationOptions.ConfigDirectoryEnvironmentVariable
        );

        List<string> candidates = [];

        if (!string.IsNullOrWhiteSpace(options.ConfigDirectory))
            candidates.Add(options.ConfigDirectory);

        if (!string.IsNullOrWhiteSpace(fromConfiguration))
            candidates.Add(fromConfiguration);

        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            candidates.Add(fromEnvironment);

        string currentDirectory = Directory.GetCurrentDirectory();
        candidates.Add(Path.Combine(currentDirectory, "config"));
        candidates.Add(Path.Combine(currentDirectory, "config.example"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "config"));

        foreach (string candidate in candidates.Select(Path.GetFullPath).Distinct())
        {
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "Services.json")))
                return candidate;
        }

        StringBuilder message = new();
        message.AppendLine("No valid configuration directory was found.");
        message.AppendLine(
            $"You can set '{RuntimeConfigurationOptions.ConfigDirectoryEnvironmentVariable}' or provide an explicit directory."
        );
        message.AppendLine("Checked directories:");

        foreach (string candidate in candidates.Select(Path.GetFullPath).Distinct())
            message.AppendLine($"- {candidate}");

        throw new DirectoryNotFoundException(message.ToString());
    }
}
