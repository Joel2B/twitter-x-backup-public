namespace Backup.Application.PostIngestion;

public class PostIngestionException(string message, Exception? innerException = null)
    : Exception(message, innerException);
