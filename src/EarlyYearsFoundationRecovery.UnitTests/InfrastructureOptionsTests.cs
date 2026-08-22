using EarlyYearsFoundationRecovery.Infrastructure;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class InfrastructureOptionsTests
{
    [Theory]
    [InlineData("http://localhost:5000", true)]
    [InlineData("https://training.example.test", true)]
    [InlineData("/relative", false)]
    [InlineData("ftp://example.test", false)]
    [InlineData("", false)]
    public void Public_base_url_must_be_absolute_http_or_https(string value, bool expected) =>
        Assert.Equal(expected, InfrastructureOptions.IsValid(new() { PublicBaseUrl = value }));
}
