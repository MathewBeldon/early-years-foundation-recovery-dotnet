using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Web.Authentication;
using EarlyYearsFoundationRecovery.Web.Filters;
using EarlyYearsFoundationRecovery.Web.Models;
using EarlyYearsFoundationRecovery.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[Authorize]
[TypeFilter(typeof(RequireRegistrationCompleteFilter))]
[Route("modules/{moduleName}/pages/{pageName}")]
public class TrainingPagesController(
    ITrainingContentProvider contentProvider,
    IUserModuleProgressRepository progressRepository,
    ITrainingAssessmentRepository assessmentRepository,
    INoteRepository noteRepository,
    IUserRepository users,
    IPdfGenerator pdfGenerator,
    ModuleProgressService moduleProgressService,
    GovUkMarkdownRenderer markdownRenderer) : Controller
{
    [HttpGet("")]
    [HttpGet("/modules/{moduleName}/content-pages/{pageName}")]
    [HttpGet("/modules/{moduleName}/assessment-result/{pageName}")]
    public async Task<IActionResult> Show(string moduleName, string pageName, CancellationToken cancellationToken)
    {
        var module = await contentProvider.GetModuleByNameAsync(moduleName, cancellationToken);
        var page = module?.PageByName(pageName);
        if (module is null || !module.Live || page is null)
        {
            return NotFound();
        }

        if (page.IsQuestion)
        {
            return Redirect(TrainingModuleContent.ContentUrl(moduleName, page));
        }

        var userId = User.GetUserId()!.Value;
        var progress = await moduleProgressService.RecordPageViewAsync(userId, module, pageName, cancellationToken);
        var assessment = await assessmentRepository.GetLatestAssessmentAsync(userId, moduleName, asNoTracking: true, cancellationToken);
        var (retakeOrResultsLabel, retakeOrResultsUrl) = ModuleProgressDisplay.BuildRetakeOrResultsLink(module, assessment);
        var nextPage = module.NextPageAfter(pageName);
        var (nextUrl, nextLabel) = PageNavigationDisplay.BuildNext(module, page, nextPage);
        var (previousUrl, previousLabel) = PageNavigationDisplay.BuildPrevious(module, page);
        var progressPercentage = moduleProgressService.CalculatePercentage(progress, module);

        var model = new TrainingPageViewModel
        {
            ModuleName = module.Name,
            ModulePosition = module.Position,
            ModuleTitle = module.Title,
            PageName = page.Name,
            PageType = page.PageType,
            Heading = page.Heading,
            Body = markdownRenderer.Render(page.Body),
            ProgressPercentage = progressPercentage,
            ProgressSummary = ModuleProgressDisplay.BuildProgressSummary(progressPercentage, assessment),
            RetakeOrResultsLabel = retakeOrResultsLabel,
            RetakeOrResultsUrl = retakeOrResultsUrl,
            NextPageUrl = nextUrl,
            NextPageLabel = nextLabel,
            PreviousPageUrl = previousUrl,
            PreviousPageLabel = previousLabel,
            BackUrl = $"/modules/{module.Name}",
            BackLinkText = PageNavigationDisplay.BuildBackLinkText(module),
            ContinueLabel = page.PageType == "certificate" ? "Back to My modules" : nextLabel,
            SupportsNotes = page.SupportsNotes,
            SectionBar = SectionBarBuilder.Build(module, page),
        };

        if (page.SupportsNotes)
        {
            var existingNote = await noteRepository.GetByUserAndPageAsync(userId, moduleName, pageName, cancellationToken);
            model.NoteForm = new LearningLogNoteFormViewModel
            {
                Body = existingNote?.Body,
                Title = page.Heading,
                TrainingModule = module.Name,
                Name = page.Name,
                NextPageName = nextPage?.Name,
                NextPageModule = module.Name,
                NextPageUrl = nextUrl,
                PageType = page.PageType,
                PreviousPageUrl = previousUrl,
                PreviousPageLabel = previousLabel,
                SubmitLabel = nextLabel,
                LearningLogAnchor = module.TabAnchor,
            };
        }

        if (page.PageType == "assessment_results")
        {
            model.AssessmentScore = assessment?.Score;
            model.AssessmentPassed = assessment?.Passed;
            model.Body = BuildAssessmentResultsBody(assessment);
        }

        if (page.PageType == "certificate")
        {
            var user = await users.GetByIdAsync(userId, cancellationToken);
            model.IsCompleted = progress?.CompletedAt is not null;
            model.AssessmentScore = assessment?.Score;
            model.AssessmentPassed = assessment?.Passed;
            model.RecipientName = GetDisplayName(user);
            model.Criteria = markdownRenderer.Render(module.Criteria);
            model.CompletedAt = progress?.CompletedAt;
            model.CertificateDownloadUrl = model.IsCompleted
                ? $"/modules/{moduleName}/certificate.pdf"
                : null;
        }

        return View(model);
    }

    [HttpGet("/modules/{moduleName}/content-pages")]
    [HttpGet("/modules/{moduleName}/pages")]
    public async Task<IActionResult> Index(string moduleName, CancellationToken cancellationToken)
    {
        var module = await contentProvider.GetModuleByNameAsync(moduleName, cancellationToken);
        if (module is null || !module.Live || module.FirstPage is null)
        {
            return NotFound();
        }

        return Redirect(TrainingModuleContent.ContentUrl(moduleName, module.FirstPage));
    }

    [HttpGet("/modules/{moduleName}/certificate.pdf")]
    public async Task<IActionResult> DownloadCertificate(string moduleName, CancellationToken cancellationToken)
    {
        var module = await contentProvider.GetModuleByNameAsync(moduleName, cancellationToken);
        if (module is null || !module.Live || module.CertificatePage is null)
        {
            return NotFound();
        }

        var userId = User.GetUserId()!.Value;
        var progress = await progressRepository.GetAsync(userId, moduleName, asNoTracking: true, cancellationToken);
        if (progress?.CompletedAt is null)
        {
            return NotFound();
        }

        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var bytes = await pdfGenerator.GenerateCertificateAsync(
            module.Title,
            GetDisplayName(user),
            cancellationToken);

        return File(bytes, "application/pdf", $"{moduleName}-certificate.pdf");
    }

    private static string GetDisplayName(Domain.Entities.User? user)
    {
        if (user is null)
        {
            return string.Empty;
        }

        var name = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? user.Email : name;
    }

    private static string BuildAssessmentResultsBody(Domain.Entities.Assessment? assessment)
    {
        if (assessment?.Score is null)
        {
            return "<p>No assessment results are available yet.</p>";
        }

        var outcome = assessment.Passed == true ? "You passed" : "You did not pass";
        var passNote = assessment.Passed == true
            ? "Well done — you can continue to your certificate."
            : "You can continue through the module. To try again, go to My modules and select Retake end of module test.";

        return $"""
            <p><strong>Score:</strong> {assessment.Score:0}%</p>
            <p><strong>Result:</strong> {outcome} (pass mark is {QuestionAnswerService.PassThreshold:0}%)</p>
            <p>{passNote}</p>
            """;
    }
}
