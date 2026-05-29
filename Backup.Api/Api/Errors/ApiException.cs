namespace Backup.Api.Errors;

public class ApiException(string message) : Exception(message);

