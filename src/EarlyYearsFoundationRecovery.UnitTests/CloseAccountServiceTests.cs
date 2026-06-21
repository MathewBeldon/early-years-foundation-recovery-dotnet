using EarlyYearsFoundationRecovery.Application.CloseAccount;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Notify;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure;
using EarlyYearsFoundationRecovery.Infrastructure.Auth;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class CloseAccountServiceTests
{
    [Fact]
    public async Task RedactAndCloseAsync_follows_rails_redaction_flow()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        var user = new User
        {
            Email = "teacher@example.com",
            FirstName = "Jane",
            LastName = "Smith",
            GovOneId = "gov-one-123",
            RegistrationComplete = true,
            ClosedReason = CloseAccountReasons.NotUseful,
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        dbContext.Notes.Add(new Note
        {
            UserId = user.Id,
            TrainingModule = "module-1",
            Name = "intro",
            Body = "Private learning note",
        });
        dbContext.Responses.Add(new Response
        {
            UserId = user.Id,
            TrainingModule = "feedback",
            QuestionName = "feedback-textarea-only",
            QuestionType = "feedback",
            TextInput = "Some free-text feedback",
            Answers = [],
        });
        dbContext.Responses.Add(new Response
        {
            UserId = user.Id,
            TrainingModule = "module-1",
            QuestionName = "formative-1",
            QuestionType = "formative",
            TextInput = "Should remain",
            Answers = ["answer-a"],
        });
        dbContext.MailEvents.Add(new MailEvent
        {
            UserId = user.Id,
            Template = "existing-template",
            Personalisation = new Dictionary<string, object?>(),
        });
        await dbContext.SaveChangesAsync();

        var notifyLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<LoggingNotifyService>();
        var notifyService = new LoggingNotifyService(dbContext, notifyLogger);
        var infrastructureOptions = Options.Create(new InfrastructureOptions());
        var service = new CloseAccountService(dbContext, notifyService, infrastructureOptions);

        await service.RedactAndCloseAsync(user.Id);

        var redactedUser = await dbContext.Users.SingleAsync();
        Assert.Equal("Redacted", redactedUser.FirstName);
        Assert.Equal("User", redactedUser.LastName);
        Assert.Equal($"redacted_user{user.Id}@example.com", redactedUser.Email);
        Assert.Equal($"{user.Id}gov-one-123", redactedUser.GovOneId);
        Assert.NotNull(redactedUser.ClosedAt);
        Assert.Equal(CloseAccountReasons.NotUseful, redactedUser.ClosedReason);

        Assert.Empty(await dbContext.Notes.ToListAsync());
        Assert.Empty(await dbContext.MailEvents.ToListAsync());

        var feedbackResponse = await dbContext.Responses.SingleAsync(r => r.QuestionType == "feedback");
        Assert.Null(feedbackResponse.TextInput);

        var formativeResponse = await dbContext.Responses.SingleAsync(r => r.QuestionType == "formative");
        Assert.Equal("Should remain", formativeResponse.TextInput);
    }

    [Fact]
    public async Task SaveCloseReasonAsync_defaults_other_reason_when_blank()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        var user = new User { Email = "teacher@example.com", RegistrationComplete = true };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new CloseAccountService(
            dbContext,
            new LoggingNotifyService(dbContext, new Microsoft.Extensions.Logging.Abstractions.NullLogger<LoggingNotifyService>()),
            Options.Create(new InfrastructureOptions()));

        await service.SaveCloseReasonAsync(user.Id, CloseAccountReasons.Other, "   ");

        var updatedUser = await dbContext.Users.SingleAsync();
        Assert.Equal(CloseAccountReasons.Other, updatedUser.ClosedReason);
        Assert.Equal("No reason provided", updatedUser.ClosedReasonCustom);
    }
}
