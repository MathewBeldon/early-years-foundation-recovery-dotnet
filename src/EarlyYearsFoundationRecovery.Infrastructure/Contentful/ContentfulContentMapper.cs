using System.Text.Json;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonPropertyAttribute = Newtonsoft.Json.JsonPropertyAttribute;

namespace EarlyYearsFoundationRecovery.Infrastructure.Contentful;

internal static class ContentfulContentMapper
{
    public static TrainingModuleContent ToModule(TrainingModuleFields fields) => new(
        fields.Name,
        fields.Title,
        fields.Description,
        fields.Outcomes,
        fields.Criteria,
        fields.Duration,
        fields.Position,
        fields.Live,
        fields.Pages.Select(ToPage).ToList(),
        string.IsNullOrWhiteSpace(fields.Upcoming) ? null : fields.Upcoming);

    public static TrainingPageContent ToPage(PageFields page) => new(
        page.Name,
        page.PageType,
        page.Heading,
        page.Body,
        ParseAnswers(page.Answers),
        page.SuccessMessage,
        page.FailureMessage,
        page.Notes);

    public static StaticPageContent ToStaticPage(StaticPageFields page) => new(
        page.Name,
        page.Title,
        page.Heading,
        page.Body,
        page.Footer,
        page.RequiresAuth);

    public static FeedbackQuestionContent ToFeedbackQuestion(QuestionFields question) => new(
        question.Name,
        question.PageType,
        question.InputType,
        question.Heading,
        question.Legend,
        question.Body,
        question.Options ?? [],
        question.Skippable ?? false,
        !string.IsNullOrWhiteSpace(question.Other),
        question.Other,
        question.More ?? false,
        !string.IsNullOrWhiteSpace(question.Or),
        question.Or);

    public static FeedbackFormContent ToFeedbackForm(IEnumerable<QuestionFields> questions) =>
        new(questions.Select(ToFeedbackQuestion).ToList());

    private static IReadOnlyList<QuestionAnswerOption> ParseAnswers(object? answers)
    {
        if (answers is null)
        {
            return [];
        }

        if (answers is JToken token)
        {
            return ParseAnswersToken(token);
        }

        if (answers is JsonElement element)
        {
            return ParseAnswersToken(JToken.Parse(element.GetRawText()));
        }

        if (answers is IEnumerable<object> objects)
        {
            return ParseAnswersToken(JToken.FromObject(objects));
        }

        return [];
    }

    private static IReadOnlyList<QuestionAnswerOption> ParseAnswersToken(JToken token)
    {
        if (token.Type != JTokenType.Array)
        {
            return [];
        }

        var options = new List<QuestionAnswerOption>();
        foreach (var item in token)
        {
            if (item is not JObject obj)
            {
                continue;
            }

            var text = obj.Value<string>("text");
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            options.Add(new QuestionAnswerOption(text, obj.Value<bool?>("correct") ?? false));
        }

        return options;
    }
}

internal sealed class TrainingModuleFields
{
    public string Title { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Outcomes { get; set; } = string.Empty;
    public string Criteria { get; set; } = string.Empty;
    public decimal Duration { get; set; }
    public int Position { get; set; }
    public bool Live { get; set; } = true;
    public string? Upcoming { get; set; }
    public List<PageFields> Pages { get; set; } = [];
}

internal sealed class PageFields
{
    public string Name { get; set; } = string.Empty;

    [JsonPropertyAttribute("page_type")]
    public string PageType { get; set; } = string.Empty;

    public string Heading { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool Notes { get; set; }
    public object? Answers { get; set; }

    [JsonPropertyAttribute("success_message")]
    public string? SuccessMessage { get; set; }

    [JsonPropertyAttribute("failure_message")]
    public string? FailureMessage { get; set; }
}

internal sealed class StaticPageFields
{
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Heading { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool Footer { get; set; }

    [JsonPropertyAttribute("requires_auth")]
    public bool RequiresAuth { get; set; }
}

internal sealed class QuestionFields
{
    public string Name { get; set; } = string.Empty;

    [JsonPropertyAttribute("page_type")]
    public string PageType { get; set; } = string.Empty;

    [JsonPropertyAttribute("input_type")]
    public string InputType { get; set; } = string.Empty;

    public string Heading { get; set; } = string.Empty;
    public string Legend { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public List<string>? Options { get; set; }
    public bool? Skippable { get; set; }
    public string? Other { get; set; }
    public string? Or { get; set; }
    public bool? More { get; set; }
}

internal sealed class CourseFields
{
    [JsonPropertyAttribute("service_name")]
    public string ServiceName { get; set; } = string.Empty;

    [JsonPropertyAttribute("internal_mailbox")]
    public string InternalMailbox { get; set; } = string.Empty;

    [JsonPropertyAttribute("privacy_policy_url")]
    public string PrivacyPolicyUrl { get; set; } = string.Empty;

    public List<QuestionFields> Feedback { get; set; } = [];
}
