using EarlyYearsFoundationRecovery.Application.Feedback;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Observability;
using EarlyYearsFoundationRecovery.Web.Authentication;
using EarlyYearsFoundationRecovery.Web.Filters;
using EarlyYearsFoundationRecovery.Web.Models;
using EarlyYearsFoundationRecovery.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[Authorize]
[TypeFilter(typeof(RequireRegistrationCompleteFilter))]
[Route("feedback")]
public class FeedbackController(
    IFeedbackContentProvider contentProvider,
    CourseFeedbackService feedbackService,
    GovUkMarkdownRenderer markdownRenderer) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId() ?? throw new InvalidOperationException("User is not authenticated.");
        var form = await contentProvider.GetFormAsync(cancellationToken);
        var isComplete = await feedbackService.IsCompleteAsync(userId, cancellationToken);

        return View(new FeedbackIndexViewModel
        {
            IsComplete = isComplete,
            FirstQuestionUrl = form.FirstQuestion is null ? null : $"/feedback/{form.FirstQuestion.Name}",
        });
    }

    [HttpGet("{questionName}")]
    public async Task<IActionResult> Show(string questionName, [FromQuery] string? from, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId() ?? throw new InvalidOperationException("User is not authenticated.");
        var form = await contentProvider.GetFormAsync(cancellationToken);
        var question = form.PageByName(questionName);

        if (question is null)
        {
            return NotFound();
        }

        if (question.IsThankYou)
        {
            return View("ThankYou", new FeedbackThankYouViewModel
            {
                Heading = question.Heading,
                Body = markdownRenderer.Render(question.Body),
            });
        }

        var isProfileUpdate = string.Equals(from, "profile", StringComparison.OrdinalIgnoreCase);
        if (isProfileUpdate)
        {
            var skippable = form.SkippableQuestion;
            if (skippable is null || !string.Equals(question.Name, skippable.Name, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect($"/feedback/{skippable?.Name}?from=profile");
            }
        }

        var existing = await feedbackService.GetResponseAsync(userId, question.Name, cancellationToken);
        var model = BuildQuestionViewModel(question, existing, form, isProfileUpdate, markdownRenderer);

        return View(model);
    }

    [HttpPost("{questionName}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        string questionName,
        FeedbackSubmitModel submission,
        CancellationToken cancellationToken)
    {
        using var activity = ApplicationTelemetry.StartJourneyActivity(
            "Feedback submitted",
            "feedback",
            "question");
        var userId = User.GetUserId() ?? throw new InvalidOperationException("User is not authenticated.");
        var form = await contentProvider.GetFormAsync(cancellationToken);
        var question = form.PageByName(questionName);

        if (question is null || question.IsThankYou)
        {
            activity?.SetTag("feedback.result", "not_found");
            ApplicationTelemetry.RecordFeedbackSubmission("not_found", "course");
            ApplicationTelemetry.MarkActivityFailure(activity, "not_found");
            return NotFound();
        }

        var isProfileUpdate = string.Equals(submission.From, "profile", StringComparison.OrdinalIgnoreCase);
        var mode = isProfileUpdate ? "profile_update" : "course";
        activity?.SetTag("feedback.mode", mode);
        activity?.SetTag("feedback.input_type", question.InputType);
        var result = await feedbackService.SaveResponseAsync(
            userId,
            question,
            submission.SelectedAnswers,
            submission.TextInput,
            cancellationToken);

        if (!result.IsValid)
        {
            activity?.SetTag("feedback.result", "validation_failed");
            ApplicationTelemetry.RecordFeedbackSubmission("validation_failed", mode);
            ApplicationTelemetry.MarkActivityFailure(activity, "validation_failed");
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Enter a valid answer.");
            var model = BuildQuestionViewModel(question, null, form, isProfileUpdate, markdownRenderer);
            model.SelectedAnswers = submission.SelectedAnswers;
            model.TextInput = submission.TextInput;
            return View("Show", model);
        }

        if (isProfileUpdate)
        {
            activity?.SetTag("feedback.result", "saved");
            activity?.SetTag("feedback.next", "my_account");
            ApplicationTelemetry.RecordFeedbackSubmission("saved", mode, "my_account");
            ApplicationTelemetry.MarkActivitySuccess(activity);
            TempData["Notice"] = "Your details have been updated";
            return Redirect("/my-account");
        }

        var next = form.NextAfter(question.Name);
        var nextStep = next is null ? "complete" : "next_question";
        activity?.SetTag("feedback.result", "saved");
        activity?.SetTag("feedback.next", nextStep);
        ApplicationTelemetry.RecordFeedbackSubmission("saved", mode, nextStep);
        ApplicationTelemetry.MarkActivitySuccess(activity);
        return Redirect(next is null ? "/feedback" : $"/feedback/{next.Name}");
    }

    private static FeedbackQuestionViewModel BuildQuestionViewModel(
        FeedbackQuestionContent question,
        Domain.Entities.Response? existing,
        FeedbackFormContent form,
        bool isProfileUpdate,
        GovUkMarkdownRenderer markdownRenderer)
    {
        var previous = form.PreviousBefore(question.Name);
        var otherIndex = question.HasOther ? Math.Max(question.Options.Count - 1, 0) : -1;
        var orIndex = question.HasOr ? Math.Max(question.Options.Count - 1, 0) : -1;

        return new FeedbackQuestionViewModel
        {
            QuestionName = question.Name,
            Heading = question.Heading,
            Legend = question.Legend,
            Body = markdownRenderer.Render(question.Body),
            InputType = question.InputType,
            Options = question.Options,
            Skippable = question.Skippable,
            HasOther = question.HasOther,
            OtherLabel = question.OtherLabel ?? "Other",
            HasMore = question.HasMore,
            HasOr = question.HasOr,
            OrLabel = question.OrLabel ?? "None of these",
            SelectedAnswers = existing?.Answers ?? [],
            TextInput = existing?.TextInput,
            PreviousUrl = previous is null ? "/feedback" : $"/feedback/{previous.Name}",
            ShowPrevious = !isProfileUpdate,
            IsProfileUpdate = isProfileUpdate,
            SubmitLabel = isProfileUpdate ? "Save" : "Next",
            OtherOptionIndex = otherIndex,
            OrOptionIndex = orIndex,
        };
    }
}
