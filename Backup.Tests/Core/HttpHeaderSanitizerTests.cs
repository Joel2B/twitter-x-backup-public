using Backup.Infrastructure.Logging;

namespace Backup.Tests;

public class HttpHeaderSanitizerTests
{
    [Fact]
    public void Sanitize_Redacts_Sensitive_Header_Values()
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["authorization"] = "Bearer secret-token",
            ["cookie"] = "auth_token=abc; ct0=def",
            ["x-csrf-token"] = "csrf-secret",
            ["x-guest-token"] = "guest-secret",
            ["user-agent"] = "Mozilla/5.0",
        };

        IReadOnlyDictionary<string, string> sanitized = HttpHeaderSanitizer.Sanitize(headers);

        Assert.Equal("[REDACTED]", sanitized["authorization"]);
        Assert.Equal("[REDACTED]", sanitized["cookie"]);
        Assert.Equal("[REDACTED]", sanitized["x-csrf-token"]);
        Assert.Equal("[REDACTED]", sanitized["x-guest-token"]);
        Assert.Equal("Mozilla/5.0", sanitized["user-agent"]);
    }

    [Fact]
    public void ToSanitizedJson_Uses_Redacted_Values()
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["authorization"] = "Bearer secret-token",
            ["user-agent"] = "Mozilla/5.0",
        };

        string json = HttpHeaderSanitizer.ToSanitizedJson(headers);

        Assert.Contains(@"""authorization"":""[REDACTED]""", json);
        Assert.Contains(@"""user-agent"":""Mozilla/5.0""", json);
        Assert.DoesNotContain("secret-token", json);
    }
}
