namespace Backup.IntegrationTests;

public sealed class LiveApiFactAttribute : FactAttribute
{
    public LiveApiFactAttribute()
    {
        if (
            !string.Equals(
                Environment.GetEnvironmentVariable("RUN_LIVE_X_API_TESTS"),
                "1",
                StringComparison.Ordinal
            )
        )
            Skip = "Set RUN_LIVE_X_API_TESTS=1 to run live API tests.";
    }
}
