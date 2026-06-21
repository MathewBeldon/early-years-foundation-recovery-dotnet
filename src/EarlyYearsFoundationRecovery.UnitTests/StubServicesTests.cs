using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class StubServicesTests
{
    [Fact]
    public async Task LoggingNotifyService_persists_mail_event()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        dbContext.Users.Add(new User { Email = "test@example.com" });
        await dbContext.SaveChangesAsync();

        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<LoggingNotifyService>();
        var service = new LoggingNotifyService(dbContext, logger);

        await service.SendEmailAsync(
            "demo-template",
            "test@example.com",
            new Dictionary<string, object?> { ["name"] = "Test User" },
            userId: 1);

        Assert.Single(await dbContext.MailEvents.ToListAsync());
    }

    [Fact]
    public async Task StubPdfGenerator_returns_certificate_bytes()
    {
        var generator = new StubPdfGenerator();
        var bytes = await generator.GenerateCertificateAsync("demo-module", "Test User");

        Assert.NotEmpty(bytes);
    }
}
