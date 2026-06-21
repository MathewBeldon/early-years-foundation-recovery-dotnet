using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class ModuleOverviewDisplayTests
{
    private static TrainingModuleContent CreateModule() =>
        new(
            Name: "understanding-development",
            Title: "Understanding child development",
            Description: "Demo",
            Outcomes: string.Empty,
            Criteria: string.Empty,
            Duration: 2,
            Position: 1,
            Live: true,
            Pages:
            [
                TrainingPageContent.CreatePage("what-to-expect", "interruption_page", "Before you begin", string.Empty),
                TrainingPageContent.CreatePage("key-concepts", "topic_intro", "Key concepts", string.Empty),
                TrainingPageContent.CreatePage("applying-learning", "text_page", "Applying learning", string.Empty),
                new(
                    "check-understanding",
                    "formative",
                    "Check your understanding",
                    string.Empty,
                    [new QuestionAnswerOption("Correct", true)],
                    "Correct",
                    "Incorrect"),
                TrainingPageContent.CreatePage("assessment-intro", "assessment_intro", "End of module assessment", string.Empty),
                new(
                    "summative-q1",
                    "summative",
                    "Question 1",
                    string.Empty,
                    [new QuestionAnswerOption("Correct", true)],
                    "Correct",
                    "Incorrect"),
                TrainingPageContent.CreatePage("assessment-results", "assessment_results", "Results", string.Empty),
                TrainingPageContent.CreatePage("certificate", "certificate", "Module complete", string.Empty),
            ]);

    [Fact]
    public void ContentSections_splits_before_certificate_page()
    {
        var module = CreateModule();

        Assert.Equal(2, module.ContentSections.Count);
        Assert.Equal("key-concepts", module.ContentSections[0][0].Name);
        Assert.Equal("assessment-results", module.ContentSections[0][^1].Name);
        Assert.Equal("certificate", module.ContentSections[1][0].Name);
    }

    [Fact]
    public void BuildSections_returns_visible_sections_with_subsections()
    {
        var module = CreateModule();
        var progress = new UserModuleProgress
        {
            ModuleName = module.Name,
            StartedAt = DateTime.UtcNow,
            VisitedPages = new Dictionary<string, bool> { ["key-concepts"] = true },
        };

        var sections = ModuleOverviewDisplay.BuildSections(
            module,
            progress,
            assessment: null,
            new ModuleProgressService(new StubProgressRepository(), new StubTrainingContentProvider(module)));

        Assert.Equal(2, sections.Count);
        Assert.Equal("Key concepts", sections[0].Heading);
        Assert.Equal(2, sections[0].Subsections.Count);
        Assert.Equal("Complete module", sections[1].Heading);
    }

    [Fact]
    public void BuildSections_marks_assessment_subsection_as_failed_when_test_failed()
    {
        var module = CreateModule();
        var progress = new UserModuleProgress
        {
            ModuleName = module.Name,
            StartedAt = DateTime.UtcNow,
            VisitedPages = module.ContentPages.ToDictionary(p => p.Name, _ => true),
        };
        var assessment = new Assessment { Score = 50, Passed = false };

        var sections = ModuleOverviewDisplay.BuildSections(
            module,
            progress,
            assessment,
            new ModuleProgressService(new StubProgressRepository(), new StubTrainingContentProvider(module)));

        var assessmentSubsection = sections[0].Subsections.Single(s => s.Heading == "End of module assessment");
        Assert.Equal(ModuleSectionStatus.Failed, assessmentSubsection.Status);
        Assert.Equal("retake test", assessmentSubsection.StatusLabel);
    }

    private sealed class StubProgressRepository : IUserModuleProgressRepository
    {
        public Task<UserModuleProgress?> GetAsync(long userId, string moduleName, bool asNoTracking = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserModuleProgress?>(null);

        public Task<IReadOnlyList<UserModuleProgress>> GetForUserAsync(long userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserModuleProgress>>([]);

        public Task<UserModuleProgress> GetOrCreateAsync(long userId, string moduleName, CancellationToken cancellationToken = default) =>
            Task.FromResult(new UserModuleProgress { UserId = userId, ModuleName = moduleName });

        public Task SaveAsync(UserModuleProgress progress, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubTrainingContentProvider(TrainingModuleContent module) : ITrainingContentProvider
    {
        public Task<IReadOnlyList<TrainingModuleContent>> GetLiveModulesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TrainingModuleContent>>([module]);

        public Task<IReadOnlyList<TrainingModuleContent>> GetAllModulesAsync(CancellationToken cancellationToken = default) =>
            GetLiveModulesAsync(cancellationToken);

        public Task<TrainingModuleContent?> GetModuleByNameAsync(string moduleName, CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Equals(module.Name, moduleName, StringComparison.OrdinalIgnoreCase) ? module : null);

        public Task<TrainingPageContent?> GetPageAsync(string moduleName, string pageName, CancellationToken cancellationToken = default) =>
            Task.FromResult(GetModuleByNameAsync(moduleName, cancellationToken).Result?.PageByName(pageName));
    }
}
