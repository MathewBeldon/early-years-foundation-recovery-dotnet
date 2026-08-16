using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class ModuleCompletionPolicyTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Summative_module_requires_a_passed_assessment(bool? passed, bool expected)
    {
        var assessment = passed is null ? null : new Assessment { Passed = passed, Score = passed.Value ? 100 : 0 };

        Assert.Equal(expected, ModuleCompletionPolicy.CanAccessCertificate(CreateModule(withAssessment: true), assessment));
    }

    [Fact]
    public void Module_without_summative_assessment_does_not_require_an_attempt()
    {
        Assert.True(ModuleCompletionPolicy.CanAccessCertificate(CreateModule(withAssessment: false), null));
    }

    [Fact]
    public void Blocked_learner_is_sent_to_the_assessment_intro()
    {
        Assert.Equal(
            "/modules/alpha/content-pages/assessment-intro",
            ModuleCompletionPolicy.BlockedCertificateDestination(CreateModule(withAssessment: true)));
    }

    private static TrainingModuleContent CreateModule(bool withAssessment)
    {
        var pages = new List<TrainingPageContent>
        {
            TrainingPageContent.CreatePage("intro", "text_page", "Intro", string.Empty),
        };
        if (withAssessment)
        {
            pages.Add(TrainingPageContent.CreatePage("assessment-intro", "assessment_intro", "Test", string.Empty));
            pages.Add(new TrainingPageContent(
                "question-1",
                "summative",
                "Question",
                string.Empty,
                [new QuestionAnswerOption("Correct", true)],
                "Correct",
                "Incorrect"));
        }

        pages.Add(TrainingPageContent.CreatePage("certificate", "certificate", "Certificate", string.Empty));
        return new TrainingModuleContent(
            "alpha", "Alpha", string.Empty, string.Empty, string.Empty, 1, 1, true, pages);
    }
}
