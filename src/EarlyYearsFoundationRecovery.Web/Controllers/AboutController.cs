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
    public IActionResult Experts()
    {
        return RedirectToAction(nameof(Course));
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
