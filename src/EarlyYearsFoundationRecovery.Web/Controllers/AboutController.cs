using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Web.Models;
using EarlyYearsFoundationRecovery.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

public class AboutController(
    ITrainingContentProvider contentProvider,
    IStaticContentProvider staticContent,
    GovUkMarkdownRenderer markdownRenderer) : Controller
{
    // Slug of the Contentful `static` entry that holds the course overview copy.
    private const string CourseOverviewSlug = "course-overview";
    private const string ExpertsSlug = "experts";
    private const string ExpertsHeading = "The experts";
    private const string ExpertsSummary = "This training course has been created by early years experts.";
    private const string ExpertsBody = """
        The experts who have worked with the Department for Education on this training have significant experience in early years education.

        Their experience includes:

        - working as early years practitioners
        - working as nursery managers
        - working in SEND residential environments
        - teaching early years courses in college environments
        - supporting a wide range of practitioners including childminders, apprentices and nursery staff

        Their qualifications in early years practice include:

        - Foundation Degree in Early Childhood Studies
        - Certificate in Education (CertEd)
        - BTEC National Diploma in EY Studies

        Their qualifications in early years management include:

        - CMI Level 5 Diploma Leadership and Management
        - CMI Level 3 Certificate in Principles of Management and Leadership
        - NCFE Level 2 Certificate in Equality and Diversity
        - NCFE Level 2 Certificate in Counselling Skills
        - Safeguarding and Prevent
        - Licence to Observe
        - D32/D33 Assessor Qualification
        """;

    [HttpGet("/about-training")]
    public async Task<IActionResult> Course(CancellationToken cancellationToken)
    {
        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        var modules = await PublicModuleViewModelBuilder.BuildAsync(contentProvider, isAuthenticated, cancellationToken);
        var allModules = await contentProvider.GetLiveModulesAsync(cancellationToken);
        var overview = await staticContent.GetPageByNameAsync(CourseOverviewSlug, cancellationToken);

        return View(new AboutCourseViewModel
        {
            Modules = modules,
            ModuleCount = allModules.Count,
            PublishedModuleCount = allModules.Count(m => m.Live),
            ActiveSection = "course",
            Title = overview?.Title ?? "About training",
            Heading = overview?.Heading ?? "About this training course",
            BodyHtml = overview is null ? string.Empty : markdownRenderer.Render(overview.Body),
        });
    }

    [HttpGet("/about/the-experts")]
    public async Task<IActionResult> Experts(CancellationToken cancellationToken)
    {
        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        var modules = await PublicModuleViewModelBuilder.BuildAsync(contentProvider, isAuthenticated, cancellationToken);
        var experts = await staticContent.GetPageByNameAsync(ExpertsSlug, cancellationToken);

        return View(new AboutExpertsViewModel
        {
            Modules = modules,
            Title = experts?.Title ?? ExpertsHeading,
            Heading = experts?.Heading ?? ExpertsHeading,
            Summary = ExpertsSummary,
            BodyHtml = markdownRenderer.Render(experts?.Body ?? ExpertsBody),
        });
    }

    [HttpGet("/about/{moduleName}")]
    public async Task<IActionResult> Show(string moduleName, CancellationToken cancellationToken)
    {
        var module = await contentProvider.GetModuleByNameAsync(moduleName, cancellationToken);
        if (module is null)
        {
            return NotFound();
        }

        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        var modules = await PublicModuleViewModelBuilder.BuildAllAsync(contentProvider, isAuthenticated, cancellationToken);
        var card = modules.FirstOrDefault(m => string.Equals(m.Name, moduleName, StringComparison.OrdinalIgnoreCase));
        if (card is null)
        {
            return NotFound();
        }

        return View(new AboutModuleViewModel
        {
            Module = card,
            AllModules = modules.Where(m => m.Live).ToList(),
            About = module.Description,
            Outcomes = markdownRenderer.Render(module.Outcomes),
            Criteria = markdownRenderer.Render(module.Criteria),
            ActiveSection = module.Name,
        });
    }
}
