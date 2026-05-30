namespace Backup.Application.Config;

public sealed class ConfigApiFileSelectionService
{
    public void ValidateApiDirectoryExists(bool exists)
    {
        if (!exists)
            throw new Exception("error deserializing config folder 'Api': directory does not exist");
    }

    public IReadOnlyDictionary<string, string> SelectRequiredFiles(
        IReadOnlyCollection<string> userIds,
        IReadOnlyCollection<string> availableFileNames
    )
    {
        if (availableFileNames.Count == 0)
            throw new Exception("error deserializing config folder 'Api': no json files found");

        HashSet<string> available = availableFileNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> selected = [];

        foreach (string userId in userIds)
        {
            string fileName = $"{userId}.json";

            if (!available.Contains(fileName))
                throw new Exception(
                    $"error deserializing config folder 'Api': file '{fileName}' not found for user '{userId}'"
                );

            selected[userId] = fileName;
        }

        return selected;
    }
}
