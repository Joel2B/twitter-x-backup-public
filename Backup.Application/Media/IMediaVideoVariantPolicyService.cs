namespace Backup.Application.Media;

public interface IMediaVideoVariantPolicyService
{
    string? GetFormatType(string contentType);
    string? GetResolution(string formatType, string url);
}
