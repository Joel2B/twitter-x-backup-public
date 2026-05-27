namespace Backup.App.Api.Errors;

public class ApiException(string message) : Exception(message);
