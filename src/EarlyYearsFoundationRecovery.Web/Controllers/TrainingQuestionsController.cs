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
[Route("modules/{moduleName}/questions/{questionName}")]
public class TrainingQuestionsController(
    ITrainingContentProvider contentProvider,
    IUserModuleProgressRepository progressRepository,
    ModuleProgressService moduleProgressService,
    QuestionAnswerService questionAnswerService,
    GovUkMarkdownRenderer markdownRenderer) : Controller
{
    [HttpGet("")]
    [HttpGet("/modules/{moduleName}/questionnaires/{questionName}")]
    public async Task<IActionResult> Show(string moduleName, string questionName, CancellationToken cancellationToken)
    {
        var module = await contentProvider.GetModuleByNameAsync(moduleName, cancellationToken);
        var question = module?.PageByName(questionName);
        if (module is null || !module.Live || question is null || !question.IsQuestion)
        {
            return NotFound();
        }

        var userId = User.GetUserId()!.Value;
        var progress = await moduleProgressService.RecordPageViewAsync(userId, module, questionName, cancellationToken);
        var existing = await questionAnswerService.GetExistingResponseAsync(userId, moduleName, questionName, cancellationToken);
        var nextPage = module.NextPageAfter(questionName);

        var model = BuildViewModel(module, question, progress, nextPage, moduleProgressService, markdownRenderer);
        if (existing is not null && question.IsFormative)
        {
            ApplyAnsweredState(
                model,
                question,
                existing.Answers.FirstOrDefault(),
                existing.Correct,
                existing.Correct == true ? question.SuccessMessage : question.FailureMessage);
        }

        return View(model);
    }

    [HttpPost("")]
    [HttpPost("/modules/{moduleName}/questionnaires/{questionName}")]
    [HttpPatch("/modules/{moduleName}/responses/{questionName}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(
        string moduleName,
        string questionName,
        [FromForm] string? selectedAnswer,
        CancellationToken cancellationToken)
    {
        var module = await contentProvider.GetModuleByNameAsync(moduleName, cancellationToken);
        var question = module?.PageByName(questionName);
        if (module is null || !module.Live || question is null || !question.IsQuestion)
        {
            return NotFound();
        }

        var userId = User.GetUserId()!.Value;
        var result = await questionAnswerService.SubmitAnswerAsync(userId, module, question, selectedAnswer ?? string.Empty, cancellationToken);

        if (!result.IsValid)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Please select an answer.");
            var progress = await progressRepository.GetAsync(userId, moduleName, asNoTracking: true, cancellationToken);
            return View("Show", BuildViewModel(
                module,
                question,
                progress,
                module.NextPageAfter(questionName),
                moduleProgressService,
                markdownRenderer));
        }

        await moduleProgressService.RecordPageViewAsync(userId, module, questionName, cancellationToken);

        if (question.IsSummative)
        {
            var nextPage = module.NextPageAfter(questionName);
            return Redirect(nextPage is null ? "/my-modules" : TrainingModuleContent.ContentUrl(module.Name, nextPage));
        }

        return RedirectToAction(nameof(Show), new { moduleName, questionName });
    }

    private static TrainingQuestionViewModel BuildViewModel(
        TrainingModuleContent module,
        TrainingPageContent question,
        Domain.Entities.UserModuleProgress? progress,
        TrainingPageContent? nextPage,
        ModuleProgressService moduleProgressService,
        GovUkMarkdownRenderer markdownRenderer)
    {
        var (nextUrl, nextLabel) = PageNavigationDisplay.BuildNext(module, question, nextPage);
        var (previousUrl, previousLabel) = PageNavigationDisplay.BuildPrevious(module, question);

        return new TrainingQuestionViewModel
        {
            ModuleName = module.Name,
            ModulePosition = module.Position,
            ModuleTitle = module.Title,
            QuestionName = question.Name,
            PageType = question.PageType,
            Heading = question.Heading,
            Body = markdownRenderer.Render(question.Body),
            ProgressPercentage = progress?.CompletedAt is not null
                ? 100
                : moduleProgressService.CalculatePercentage(progress, module),
            Answers = MapAnswerOptions(question),
            NextPageUrl = nextUrl,
            NextPageLabel = nextLabel,
            PreviousPageUrl = previousUrl,
            PreviousPageLabel = previousLabel,
            BackUrl = $"/modules/{module.Name}",
            BackLinkText = PageNavigationDisplay.BuildBackLinkText(module),
            IsFormative = question.IsFormative,
            SubmitLabel = FormativeQuestionDisplay.ResolveSubmitLabel(question),
            SectionBar = SectionBarBuilder.Build(module, question),
        };
    }

    private static void ApplyAnsweredState(
        TrainingQuestionViewModel model,
        TrainingPageContent question,
        string? selectedAnswer,
        bool? isCorrect,
        string? feedbackMessage)
    {
        var (bannerTitle, bannerCssClass) = FormativeQuestionDisplay.BuildBanner(isCorrect);
        model.SelectedAnswer = selectedAnswer;
        model.ShowFeedback = true;
        model.IsCorrect = isCorrect;
        model.FeedbackMessage = feedbackMessage;
        model.CanSubmit = false;
        model.BannerTitle = bannerTitle;
        model.BannerCssClass = bannerCssClass;
        model.Answers = MapAnswerOptions(question, selectedAnswer, responded: true);
    }

    private static IReadOnlyList<QuestionAnswerOptionViewModel> MapAnswerOptions(
        TrainingPageContent question,
        string? selectedAnswer = null,
        bool responded = false) =>
        FormativeQuestionDisplay.BuildAnswerOptions(question, selectedAnswer, responded)
            .Select(option => new QuestionAnswerOptionViewModel
            {
                Text = option.Text,
                Correct = option.Correct,
                Checked = option.Checked,
                Disabled = option.Disabled,
                StatusHint = option.StatusHint,
                EmphasiseLabel = option.EmphasiseLabel,
            })
            .ToList();
}
