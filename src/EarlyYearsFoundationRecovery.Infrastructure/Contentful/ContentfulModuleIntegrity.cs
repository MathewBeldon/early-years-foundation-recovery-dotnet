using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EarlyYearsFoundationRecovery.Infrastructure.Contentful;

/// <summary>
/// The Contentful boundary equivalent of the pinned Rails module availability check.
/// </summary>
/// <remarks>
/// Rails v1.5.0 (ac5467218a49c9de58a32a69d4edc01ce37710cf) uses
/// <c>Training::Module#draft?</c> in app/models/training/module.rb:151-154, which
/// returns the inverse of <c>ContentIntegrity#valid?</c>. The individual checks are
/// from app/services/content_integrity.rb:55-57 and :78-227.
///
/// This operates on the mapped Contentful response, so it cannot perform Rails'
/// optional asset-existence lookup (app/services/content_integrity.rb:78-84 also
/// deliberately omits that network check). An image link being present is the
/// faithful boundary-level equivalent. Missing or malformed fields/pages fail
/// closed; the mapper still returns the same content shape with Live=false.
/// </remarks>
internal static class ContentfulModuleIntegrity
{
    public static bool IsValid(TrainingModuleFields module, IReadOnlyList<PageFields> pages)
    {
        if (!HasModuleFields(module) || pages.Count == 0)
        {
            return false;
        }

        var hasPreConfidence = pages.Any(page => IsType(page, "pre_confidence"));
        var content = pages.Where(page => !IsType(page, "interruption_page")).ToList();
        var submoduleCount = CountSections(content);
        var topicCount = CountSubsections(content) - submoduleCount;

        return HasType(pages, "text_page")
            && HasType(pages, "video_page")
            && HasType(pages, "assessment_intro")
            && HasType(pages, "confidence_intro")
            && HasType(pages, "recap_page")
            && HasType(pages, "summary_intro")
            && HasType(pages, "assessment_results")
            && PageAt(pages, 0, "interruption_page")
            && PageAt(pages, 1, hasPreConfidence ? "text_page" : "sub_module_intro")
            && PageAt(pages, 2, hasPreConfidence ? "pre_confidence" : "topic_intro")
            && topicCount >= submoduleCount
            && PageAt(pages, -2, "thankyou")
            && PageAt(pages, -1, "certificate")
            && HasType(pages, "formative")
            && HasType(pages, "feedback")
            && pages.Count(page => IsType(page, "summative")) == 10
            && pages.Count(page => IsType(page, "confidence")) >= 4
            && (!hasPreConfidence || pages.Count(page => IsType(page, "pre_confidence")) >= 4)
            && FactualAnswersAreValid(pages);
    }

    private static bool HasModuleFields(TrainingModuleFields module) =>
        Present(module.Upcoming)
        && Present(module.About)
        && Present(module.Description)
        && Present(module.Criteria)
        && Present(module.Outcomes)
        && module.Duration.HasValue
        && module.Position.HasValue
        && module.Image is not null;

    private static bool FactualAnswersAreValid(IEnumerable<PageFields> pages) =>
        pages
            .Where(page => IsType(page, "formative") || IsType(page, "summative"))
            .All(page => HasValidAnswerOptions(page.Answers));

    private static bool HasValidAnswerOptions(object? answers)
    {
        // Mirrors Training::Question#json in app/models/training/question.rb:200-206:
        // an absent Contentful answers field receives Rails' two-option draft default.
        if (answers is null || (answers is JToken nullToken && nullToken.Type == JTokenType.Null))
        {
            return true;
        }

        try
        {
            var answerToken = answers switch
            {
                JToken tokenValue => tokenValue,
                System.Text.Json.JsonElement element => JToken.Parse(element.GetRawText()),
                _ => JToken.FromObject(answers),
            };

            if (answerToken.Type != JTokenType.Array || answerToken.Children().Count() < 2)
            {
                return false;
            }

            var correctCount = 0;
            foreach (var option in answerToken)
            {
                if (option is not JArray values
                    || values.Count == 0
                    || values[0]?.Type != JTokenType.String
                    || string.IsNullOrWhiteSpace(values[0]!.Value<string>()))
                {
                    return false;
                }

                if (values.Count > 1 && values[1]?.Type == JTokenType.Boolean && values[1]!.Value<bool>())
                {
                    correctCount++;
                }
            }

            return correctCount >= 1;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static int CountSections(IReadOnlyList<PageFields> content)
    {
        var count = 0;
        var previousWasFeedback = false;
        foreach (var page in content)
        {
            var isFirstFeedback = IsType(page, "feedback") && !previousWasFeedback;
            if (IsType(page, "sub_module_intro")
                || IsType(page, "summary_intro")
                || IsType(page, "certificate")
                || isFirstFeedback)
            {
                count++;
            }

            previousWasFeedback = IsType(page, "feedback");
        }

        return count;
    }

    private static int CountSubsections(IReadOnlyList<PageFields> content)
    {
        var count = 0;
        var startsSection = true;
        var previousWasFeedback = false;
        foreach (var page in content)
        {
            var isFirstFeedback = IsType(page, "feedback") && !previousWasFeedback;
            var isSection = IsType(page, "sub_module_intro")
                || IsType(page, "summary_intro")
                || IsType(page, "certificate")
                || isFirstFeedback;
            var isSubsection = IsType(page, "topic_intro")
                || IsType(page, "recap_page")
                || IsType(page, "assessment_intro")
                || IsType(page, "confidence_intro")
                || IsType(page, "certificate");

            if (isSection)
            {
                startsSection = true;
            }

            if (startsSection || isSubsection)
            {
                count++;
                startsSection = false;
            }

            previousWasFeedback = IsType(page, "feedback");
        }

        return count;
    }

    private static bool HasType(IEnumerable<PageFields> pages, string type) => pages.Any(page => IsType(page, type));

    private static bool PageAt(IReadOnlyList<PageFields> pages, int index, string type)
    {
        var actualIndex = index < 0 ? pages.Count + index : index;
        return actualIndex >= 0 && actualIndex < pages.Count && IsType(pages[actualIndex], type);
    }

    private static bool IsType(PageFields page, string type) =>
        string.Equals(page.PageType, type, StringComparison.Ordinal);

    private static bool Present(string? value) => !string.IsNullOrWhiteSpace(value);
}
