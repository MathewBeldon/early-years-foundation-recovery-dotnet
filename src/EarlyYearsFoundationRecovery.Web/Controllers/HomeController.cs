using System.Diagnostics;
using System.Text.Json;
using EarlyYearsFoundationRecovery.Application.Feedback;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Web.Authentication;
using EarlyYearsFoundationRecovery.Web.Models;
using EarlyYearsFoundationRecovery.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

public class HomeController(
    ApplicationDbContext dbContext,
    ITrainingContentProvider contentProvider,
    CourseFeedbackService feedbackService,
    IOptions<AuditOptions> auditOptions,
    BotAuthenticationFailureTracker failureTracker) : Controller
{
    private const string AuditAuthenticationScope = "audit";

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        var modules = await PublicModuleViewModelBuilder.BuildAsync(contentProvider, isAuthenticated, cancellationToken);
        var showFeedbackCta = false;
        if (isAuthenticated && User.GetUserId() is { } userId)
        {
            showFeedbackCta = !await feedbackService.IsCompleteAsync(userId, cancellationToken);
        }

        return View(new PublicHomeViewModel
        {
            Modules = modules,
            IsAuthenticated = isAuthenticated,
            ShowFeedbackCta = showFeedbackCta,
        });
    }

    [HttpGet("/health")]
    public async Task<IActionResult> Health(CancellationToken cancellationToken)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

        var payload = new
        {
            status = canConnect ? "ok" : "degraded",
            database = canConnect ? "connected" : "unavailable",
            timestamp = DateTime.UtcNow,
        };

        return Content(JsonSerializer.Serialize(payload), "application/json");
    }

    [HttpGet("/audit")]
    public IActionResult Audit()
    {
        var authenticationFailure = this.EnforceBotAuthentication(
            failureTracker,
            AuditAuthenticationScope,
            BotAuthentication.SecretsMatch(Request.Headers["BOT"].FirstOrDefault(), auditOptions.Value.BotToken));
        return authenticationFailure ?? Content("BOT ACCESS GRANTED", "text/plain");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
