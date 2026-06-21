using EarlyYearsFoundationRecovery.Application.CloseAccount;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Notify;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EarlyYearsFoundationRecovery.Infrastructure.Auth;

public sealed class CloseAccountService(
    ApplicationDbContext dbContext,
    INotifyService notifyService,
    IOptions<InfrastructureOptions> options) : ICloseAccountService
{
    public async Task SaveCloseReasonAsync(
        long userId,
        string closedReason,
        string? closedReasonCustom,
        CancellationToken cancellationToken = default)
    {
        if (!CloseAccountReasons.All.Contains(closedReason))
        {
            throw new ArgumentException("Select a reason for closing your account.", nameof(closedReason));
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        user.ClosedReason = closedReason;
        user.ClosedReasonCustom = closedReason == CloseAccountReasons.Other
            ? string.IsNullOrWhiteSpace(closedReasonCustom) ? "No reason provided" : closedReasonCustom.Trim()
            : null;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<User> RedactAndCloseAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .Include(u => u.Notes)
            .Include(u => u.MailEvents)
            .Include(u => u.Responses)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (string.IsNullOrWhiteSpace(user.ClosedReason))
        {
            throw new InvalidOperationException("Close reason must be saved before closing the account.");
        }

        if (user.ClosedAt is not null)
        {
            throw new InvalidOperationException("Account is already closed.");
        }

        // Send notifications and redact inside one transaction so a partial failure can't leave the
        // account half-closed (e.g. notes removed but the user not marked as closed).
        if (dbContext.Database.IsRelational())
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                await RedactAsync(user, userId, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            });
        }
        else
        {
            await RedactAsync(user, userId, cancellationToken);
        }

        return user;
    }

    private async Task RedactAsync(User user, long userId, CancellationToken cancellationToken)
    {
        var recipientName = GetDisplayName(user);
        var recipientEmail = user.Email;
        var internalMailbox = options.Value.InternalMailbox;

        await notifyService.SendEmailAsync(
            NotifyTemplateIds.AccountClosed,
            recipientEmail,
            new Dictionary<string, object?>
            {
                ["name"] = recipientName,
                ["email"] = recipientEmail,
            },
            userId,
            cancellationToken);

        await notifyService.SendEmailAsync(
            NotifyTemplateIds.AccountClosedInternal,
            internalMailbox,
            new Dictionary<string, object?>
            {
                ["user_email_address"] = recipientEmail,
                ["email"] = internalMailbox,
            },
            userId,
            cancellationToken);

        foreach (var response in user.Responses.Where(r => r.QuestionType == "feedback"))
        {
            response.TextInput = null;
        }

        dbContext.Notes.RemoveRange(user.Notes);
        dbContext.MailEvents.RemoveRange(user.MailEvents);

        user.GovOneId = $"{user.Id}{user.GovOneId ?? string.Empty}";
        user.FirstName = "Redacted";
        user.LastName = "User";
        user.Email = $"redacted_user{user.Id}@example.com";
        user.ClosedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string GetDisplayName(User user)
    {
        var name = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? user.Email : name;
    }
}
