using System.Text.Json;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Infrastructure.Contentful;
using Newtonsoft.Json.Linq;

namespace EarlyYearsFoundationRecovery.UnitTests;

// Contract source: Rails v1.5.0 at
// ac5467218a49c9de58a32a69d4edc01ce37710cf,
// app/models/training/question.rb:195-207 and
// app/models/training/answer.rb:4-15,53-63.
public sealed class ContentfulAnswerMappingTests
{
    [Fact]
    public void Maps_the_pinned_Rails_array_answer_shape()
    {
        var page = Page("summative", JArray.Parse("[[\"Wrong\"],[\"Correct\",true]]"));

        var mapped = ContentfulContentMapper.ToPage(page);

        Assert.Equal(
            [
                new QuestionAnswerOption("Wrong", false),
                new QuestionAnswerOption("Correct", true),
            ],
            mapped.Answers);
    }

    [Fact]
    public void Retains_legacy_object_answer_compatibility()
    {
        var page = Page("formative", JArray.Parse("[{\"text\":\"Wrong\"},{\"text\":\"Correct\",\"correct\":true}]"));

        var mapped = ContentfulContentMapper.ToPage(page);

        Assert.Equal(
            [
                new QuestionAnswerOption("Wrong", false),
                new QuestionAnswerOption("Correct", true),
            ],
            mapped.Answers);
    }

    [Fact]
    public void Uses_Rails_draft_options_for_absent_or_null_factual_answers()
    {
        var absent = ContentfulContentMapper.ToPage(Page("formative"));
        var nullToken = ContentfulContentMapper.ToPage(Page("summative", JValue.CreateNull()));
        using var jsonDocument = JsonDocument.Parse("null");
        var nullJsonElement = ContentfulContentMapper.ToPage(Page("summative", jsonDocument.RootElement));

        Assert.Equal(
            [
                new QuestionAnswerOption("Wrong answer", false),
                new QuestionAnswerOption("Correct answer", true),
            ],
            absent.Answers);
        Assert.Equal(absent.Answers, nullToken.Answers);
        Assert.Equal(absent.Answers, nullJsonElement.Answers);
    }

    [Fact]
    public void Does_not_apply_factual_draft_options_to_non_factual_pages()
    {
        var mapped = ContentfulContentMapper.ToPage(Page("text_page"));

        Assert.Empty(mapped.Answers);
    }

    [Fact]
    public void Does_not_treat_an_empty_answers_array_as_missing()
    {
        var mapped = ContentfulContentMapper.ToPage(Page("summative", new JArray()));

        Assert.Empty(mapped.Answers);
    }

    [Fact]
    public void Preserves_multiple_correct_Rails_array_answers()
    {
        var page = Page("summative", JArray.Parse("[[\"Correct 1\",true],[\"Correct 2\",true],[\"Wrong\",false]]"));

        var mapped = ContentfulContentMapper.ToPage(page);

        Assert.Equal(2, mapped.Answers.Count(answer => answer.Correct));
        Assert.Equal(3, mapped.Answers.Count);
    }

    [Fact]
    public void Applies_false_default_for_missing_null_and_false_correctness_values()
    {
        var page = Page("summative", JArray.Parse("[[\"Missing\"],[\"Null\",null],[\"False\",false],[\"True\",true]]"));

        var mapped = ContentfulContentMapper.ToPage(page);

        Assert.Equal([false, false, false, true], mapped.Answers.Select(answer => answer.Correct));
    }

    [Fact]
    public void Malformed_answers_fail_safely_without_throwing()
    {
        var malformed = Page("summative", JArray.Parse("[[null],[\"\",true],{\"text\":\"invalid\",\"correct\":\"not-a-bool\"},{\"text\":\"valid\"},7]"));
        var invalidJsonElement = Page("summative", JsonDocument.Parse("{\"not\":\"an answer array\"}").RootElement);

        var mappedMalformed = ContentfulContentMapper.ToPage(malformed);
        var mappedInvalidJson = ContentfulContentMapper.ToPage(invalidJsonElement);

        Assert.Single(mappedMalformed.Answers);
        Assert.Equal("valid", mappedMalformed.Answers[0].Text);
        Assert.Empty(mappedInvalidJson.Answers);
    }

    private static PageFields Page(string pageType, object? answers = null) => new()
    {
        Name = "question-1",
        PageType = pageType,
        Heading = "Question",
        Body = "Question body",
        Answers = answers,
    };
}
