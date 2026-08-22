using System.Text.Json;
using Contentful.Core.Models;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonPropertyAttribute = Newtonsoft.Json.JsonPropertyAttribute;

namespace EarlyYearsFoundationRecovery.Infrastructure.Contentful;

internal static class ContentfulContentMapper
{
    private static readonly IReadOnlyList<QuestionAnswerOption> DraftOptions =
    [
        new QuestionAnswerOption("Wrong answer", false),
        new QuestionAnswerOption("Correct answer", true),
    ];

    public static TrainingModuleContent ToModule(TrainingModuleFields fields)
    {
        var pages = fields.Pages ?? [];
        return new TrainingModuleContent(
            fields.Name,
            fields.Title,
            fields.Description ?? string.Empty,
            fields.Outcomes ?? string.Empty,
            fields.Criteria ?? string.Empty,
            fields.Duration ?? 0,
            fields.Position ?? 0,
            ContentfulModuleIntegrity.IsValid(fields, pages),
            pages.Select(ToPage).ToList(),
            string.IsNullOrWhiteSpace(fields.Upcoming) ? null : fields.Upcoming,
            fields.Sys?.Id);
    }

    public static TrainingPageContent ToPage(PageFields page) => new(
        page.Name,
        page.PageType,
        page.Heading,
        page.Body,
        ParseAnswers(page.PageType, page.Answers),
        page.SuccessMessage,
        page.FailureMessage,
        page.Notes,
        page.Sys?.Id,
        page.MultiSelect ?? false,
        page.More ?? false,
        page.Other,
        page.Or,
        page.Skippable ?? false);

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

    private static IReadOnlyList<QuestionAnswerOption> ParseAnswers(string pageType, object? answers)
    {
        // Rails v1.5.0 (ac5467218a49c9de58a32a69d4edc01ce37710cf),
        // app/models/training/question.rb:203-207, uses DRAFT_OPTIONS for
        // nil factual answers. An empty array is intentionally not treated as nil.
        if (answers is null
            || answers is JToken { Type: JTokenType.Null }
            || answers is JsonElement { ValueKind: JsonValueKind.Null })
        {
            return IsFactualPage(pageType) ? DraftOptions : [];
        }

        try
        {
            if (answers is JToken token)
            {
                return ParseAnswersToken(token);
            }

            if (answers is JsonElement element)
            {
                return ParseAnswersToken(JToken.Parse(element.GetRawText()));
            }

            if (answers is System.Collections.IEnumerable enumerable && answers is not string)
            {
                return ParseAnswersToken(JToken.FromObject(enumerable));
            }
        }
        catch (Newtonsoft.Json.JsonException)
        {
            // Contentful data is external input. A malformed answer field must
            // not make the provider request fail.
            return [];
        }
        catch (InvalidOperationException)
        {
            return [];
        }
        catch (ArgumentException)
        {
            return [];
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
            if (item is JArray values)
            {
                var text = values.ElementAtOrDefault(0);
                if (text?.Type != JTokenType.String || string.IsNullOrWhiteSpace(text.Value<string>()))
                {
                    continue;
                }

                // Rails' Training::Answer::Option uses Params::Bool.fallback(false),
                // so a missing/null/non-boolean second value is incorrect.
                var correct = values.ElementAtOrDefault(1)?.Type == JTokenType.Boolean
                    && values[1]!.Value<bool>();
                options.Add(new QuestionAnswerOption(text.Value<string>()!, correct));
                continue;
            }

            if (item is not JObject obj)
            {
                continue;
            }

            try
            {
                var textValue = obj.Value<string>("text");
                if (string.IsNullOrWhiteSpace(textValue))
                {
                    continue;
                }

                options.Add(new QuestionAnswerOption(textValue, obj.Value<bool?>("correct") ?? false));
            }
            catch (FormatException)
            {
                // Preserve the established object-shape behaviour for valid
                // entries while safely ignoring malformed external values.
            }
            catch (ArgumentException)
            {
                // Preserve the established object-shape behaviour for valid
                // entries while safely ignoring malformed external values.
            }
            catch (InvalidCastException)
            {
                // Preserve the established object-shape behaviour for valid
                // entries while safely ignoring malformed external values.
            }
        }

        return options;
    }

    private static bool IsFactualPage(string pageType) =>
        pageType is "formative" or "summative";
}

internal sealed class TrainingModuleFields
{
    public SystemProperties? Sys { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Outcomes { get; set; }
    public string? Criteria { get; set; }
    public decimal? Duration { get; set; }
    public int? Position { get; set; }
    public string? About { get; set; }
    public object? Image { get; set; }
    public string? Upcoming { get; set; }
    public List<PageFields>? Pages { get; set; } = [];
}

internal sealed class PageFields
{
    public SystemProperties? Sys { get; set; }
    public string Name { get; set; } = string.Empty;

    [JsonPropertyAttribute("page_type")]
    public string PageType { get; set; } = string.Empty;

    public string Heading { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool Notes { get; set; }
    public object? Answers { get; set; }
    [JsonPropertyAttribute("multi_select")]
    public bool? MultiSelect { get; set; }
    public bool? More { get; set; }
    public string? Other { get; set; }
    public string? Or { get; set; }
    public bool? Skippable { get; set; }

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
