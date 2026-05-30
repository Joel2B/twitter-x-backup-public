namespace Backup.Application.Media.Filter;

public sealed record MediaExclusionRule(string Extension, string FormatType, string ResolutionName);
