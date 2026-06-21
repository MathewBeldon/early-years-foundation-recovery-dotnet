namespace EarlyYearsFoundationRecovery.Web.Models;

public class SiteHeaderViewModel
{
    public bool IsAuthenticated { get; set; }
    public bool RegistrationComplete { get; set; }
    public bool ShowSignInLink { get; set; }
    public bool ShowMyAccountLink { get; set; }
}

public class SiteFooterViewModel
{
    public IReadOnlyList<SiteFooterLinkViewModel> FooterPages { get; set; } = [];
    public string PrivacyPolicyUrl { get; set; } = string.Empty;
}

public record SiteFooterLinkViewModel(string Text, string Href);

public class SiteNavigationViewModel
{
    public bool IsAuthenticated { get; set; }
    public string CurrentPath { get; set; } = "/";
    public bool HomeNavActive { get; set; }
    public bool ModulesNavActive { get; set; }
    public bool MyModulesNavActive { get; set; }
    public bool LearningLogNavActive { get; set; }
    public bool ShowLearningLogNav { get; set; }
}

public class PublicModuleCardViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Position { get; set; }
    public bool Live { get; set; } = true;
    public string TabLabel => $"Module {Position}";
    public string AboutUrl => $"/about/{Name}";
    public string ModuleUrl { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
}

public class AboutCourseViewModel
{
    public IReadOnlyList<PublicModuleCardViewModel> Modules { get; set; } = [];
    public int ModuleCount { get; set; }
    public int PublishedModuleCount { get; set; }
    public string ActiveSection { get; set; } = "course";
    public string Heading { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
}

public class AboutModuleViewModel
{
    public PublicModuleCardViewModel Module { get; set; } = new();
    public IReadOnlyList<PublicModuleCardViewModel> AllModules { get; set; } = [];
    public string About { get; set; } = string.Empty;
    public string Outcomes { get; set; } = string.Empty;
    public string Criteria { get; set; } = string.Empty;
    public string ActiveSection { get; set; } = string.Empty;
}

public class PublicHomeViewModel
{
    public IReadOnlyList<PublicModuleCardViewModel> Modules { get; set; } = [];
    public bool IsAuthenticated { get; set; }
    public bool ShowFeedbackCta { get; set; }
}
