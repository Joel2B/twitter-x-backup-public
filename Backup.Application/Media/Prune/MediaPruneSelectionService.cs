namespace Backup.Application.Media.Prune;

public sealed class MediaPruneSelectionService(IMediaPrunePolicyService prunePolicyService)
    : IMediaPruneSelectionService
{
    private readonly IMediaPrunePolicyService _prunePolicyService = prunePolicyService;

    public bool ShouldRemove(string url, string path)
    {
        Uri uri = new(url);

        string extension = Path.GetExtension(uri.AbsolutePath);

        if (string.IsNullOrEmpty(extension))
            extension = Path.GetExtension(path);

        extension = extension.Trim('.');

        Dictionary<string, string> query = ParseQuery(uri.Query);
        query.TryGetValue("format", out string? format);
        query.TryGetValue("name", out string? name);

        return !_prunePolicyService.ShouldKeep(
            extension,
            format ?? string.Empty,
            name ?? string.Empty
        );
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(query))
            return values;

        ReadOnlySpan<char> span = query.AsSpan().TrimStart('?');

        while (!span.IsEmpty)
        {
            int andIndex = span.IndexOf('&');
            ReadOnlySpan<char> pair = andIndex >= 0 ? span[..andIndex] : span;
            span = andIndex >= 0 ? span[(andIndex + 1)..] : [];

            if (pair.IsEmpty)
                continue;

            int equalIndex = pair.IndexOf('=');
            ReadOnlySpan<char> keySpan = equalIndex >= 0 ? pair[..equalIndex] : pair;
            ReadOnlySpan<char> valueSpan = equalIndex >= 0 ? pair[(equalIndex + 1)..] : [];

            string key = Uri.UnescapeDataString(keySpan.ToString());
            string value = Uri.UnescapeDataString(valueSpan.ToString());

            if (!string.IsNullOrWhiteSpace(key))
                values[key] = value;
        }

        return values;
    }
}
