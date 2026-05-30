using System.Text.RegularExpressions;
using Backup.Application.Bulk.Models;

namespace Backup.Application.Bulk;

public sealed partial class BulkSourceExtractionService : IBulkSourceExtractionService
{
    public IReadOnlyList<BulkSourceLinkItem> Extract(IEnumerable<string> lines)
    {
        Dictionary<string, BulkSourceLinkItem> data = [];

        foreach (string line in lines)
        {
            foreach (Match match in SourceRegex().Matches(line))
            {
                if (!match.Success)
                    continue;

                string link = match.Groups["link"].Value;
                string user = match.Groups["user"].Value;
                string type = match.Groups["type"].Value;

                if (string.IsNullOrEmpty(link) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(type))
                    continue;

                BulkSourceLinkItem source = new()
                {
                    Link = link,
                    UserName = user,
                    Type = GetType(type),
                };

                data.TryAdd(user, source);
            }
        }

        return [.. data.Values];
    }

    private static BulkSourceType GetType(string type) =>
        type switch
        {
            "media" => BulkSourceType.Media,
            "status" => BulkSourceType.Status,
            _ => BulkSourceType.None,
        };

    [GeneratedRegex(
        @"(?<link>https?:\/\/(?:www\.)?x\.com\/(?<user>[^\/\s?#""'\\<>]+)(?:\/(?<type>[^\/\s?#""'\\<>]+))?(?:\/[^\s""'<>\\]*)?(?:\?[^\s""'<>\\#]*)?(?:\#[^\s""'<>\\]*)?)(?=[\s""'<>),;]|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.ECMAScript
    )]
    private static partial Regex SourceRegex();
}
