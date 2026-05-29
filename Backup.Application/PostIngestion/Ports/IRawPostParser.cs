using Backup.Application.PostIngestion.Models;

namespace Backup.Application.PostIngestion.Ports;

public interface IRawPostParser
{
    RawPostParseResult Parse(string userId, string origin, string rawRequestBody);
}
