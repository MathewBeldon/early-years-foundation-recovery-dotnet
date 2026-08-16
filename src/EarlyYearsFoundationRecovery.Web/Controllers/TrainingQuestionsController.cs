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
    QuestionnaireEventTracker questionnaireEvents,
    GovUkMarkdownRenderer markdownRenderer) : Controller
{
    private const string SubmissionNonceSessionKey = "QuestionnaireSubmissionNonce";

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

        var nonce = GetOrCreateSubmissionNonce();
        var model = BuildViewModel(module, question, progress, nextPage, moduleProgressService, markdownRenderer, nonce);
        if (existing is not null && question.IsFormative)
        {
            ApplyAnsweredState(
                model,
                question,
                SelectedAnswerText(question, existing.Answers.FirstOrDefault()),
                existing.Correct,
                existing.Correct == true ? question.SuccessMessage : question.FailureMessage);
        }

        if (question.IsSummative && ReferenceEquals(question, module.SummativeQuestions.FirstOrDefault()))
        {
            await questionnaireEvents.TrackAssessmentStartAsync(HttpContext, userId, module, question, cancellationToken);
        }

        return View(model);
    }

    [HttpPost("")]
    [HttpPost("/modules/{moduleName}/questionnaires/{questionName}")]
    [HttpPost("/modules/{moduleName}/responses/{questionName}")]
    [HttpPatch("/modules/{moduleName}/responses/{questionName}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(
        string moduleName,
        string questionName,
        [FromForm] string? selectedAnswer,
        [FromForm(Name = "response[answers]")] string? responseAnswer,
        [FromForm(Name = "response[submission_nonce]")] string? submissionNonce,
        CancellationToken cancellationToken)
    {
        var module = await contentProvider.GetModuleByNameAsync(moduleName, cancellationToken);
        var question = module?.PageByName(questionName);
        if (module is null || !module.Live || question is null || !question.IsQuestion)
        {
            return NotFound();
        }

        var userId = User.GetUserId()!.Value;
        if (question.IsSummative && !IsValidSubmissionNonce(submissionNonce))
        {
            return Redirect($"/modules/{moduleName}/questionnaires/{questionName}");
        }

        var submittedAnswer = responseAnswer ?? selectedAnswer ?? string.Empty;
        var result = await questionAnswerService.SubmitAnswerAsync(userId, module, question, submittedAnswer, cancellationToken);

        if (!result.IsValid)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Please select an answer.");
            var nonce = question.IsSummative ? ReplaceSubmissionNonce() : GetOrCreateSubmissionNonce();
            var progress = await progressRepository.GetAsync(userId, moduleName, asNoTracking: true, cancellationToken);
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return View("Show", BuildViewModel(
                module,
                question,
                progress,
                module.NextPageAfter(questionName),
                moduleProgressService,
                markdownRenderer,
                nonce));
        }

        await moduleProgressService.RecordPageViewAsync(userId, module, questionName, cancellationToken);

        if (question.IsSummative)
        {
            await questionnaireEvents.TrackAnswerAsync(
                HttpContext,
                userId,
                module,
                question,
                result.AnswerId!.Value,
                result.IsCorrect == true,
                cancellationToken);
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
        GovUkMarkdownRenderer markdownRenderer,
        string submissionNonce)
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
            Heading = QuestionLegend.For(question),
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
            SubmissionNonce = submissionNonce,
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
            .Select((option, index) => new QuestionAnswerOptionViewModel
            {
                Value = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Text = option.Text,
                Correct = option.Correct,
                Checked = option.Checked,
                Disabled = option.Disabled,
                StatusHint = option.StatusHint,
                EmphasiseLabel = option.EmphasiseLabel,
            })
            .ToList();

    private string GetOrCreateSubmissionNonce()
    {
        if (HttpContext.Session.GetString(SubmissionNonceSessionKey) is { Length: > 0 } nonce)
        {
            return nonce;
        }

        return ReplaceSubmissionNonce();
    }

    private string ReplaceSubmissionNonce()
    {
        var nonce = Guid.NewGuid().ToString();
        HttpContext.Session.SetString(SubmissionNonceSessionKey, nonce);
        return nonce;
    }

    private bool IsValidSubmissionNonce(string? nonce) =>
        !string.IsNullOrWhiteSpace(nonce)
        && string.Equals(
            HttpContext.Session.GetString(SubmissionNonceSessionKey),
            nonce,
            StringComparison.Ordinal);

    private static string? SelectedAnswerText(TrainingPageContent question, string? storedAnswer)
    {
        if (int.TryParse(storedAnswer, out var answerId)
            && answerId >= 1
            && answerId <= question.Answers.Count)
        {
            return question.Answers[answerId - 1].Text;
        }

        return storedAnswer;
    }
}
