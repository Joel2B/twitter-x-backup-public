using Backup.Application.Media.Prune;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace Backup.Infrastructure.Media.Services;

public class MediaPrune(IMediaPrunePolicyService prunePolicyService) : IMediaPrune
{
    private readonly IMediaPrunePolicyService _prunePolicyService = prunePolicyService;

    public Task Prune(List<Download> downloads)
    {
        foreach (Download download in downloads)
        {
            download.Data.RemoveAll(data =>
            {
                Uri uri = new(data.Url);

                string extension = Path.GetExtension(uri.AbsolutePath);

                if (string.IsNullOrEmpty(extension))
                    extension = Path.GetExtension(data.Path);

                extension = extension.Trim('.');

                var query = QueryHelpers.ParseQuery(uri.Query);

                string format = query.TryGetValue("format", out var formatValue)
                    ? formatValue.ToString()
                    : string.Empty;
                string name = query.TryGetValue("name", out var nameValue)
                    ? nameValue.ToString()
                    : string.Empty;

                return !_prunePolicyService.ShouldKeep(extension, format, name);
            });
        }

        downloads.RemoveAll(dl => dl.Data.Count == 0);

        return Task.CompletedTask;
    }
}
