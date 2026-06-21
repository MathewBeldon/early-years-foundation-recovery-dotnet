namespace EarlyYearsFoundationRecovery.Web.Models;

public class StaticPageViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Heading { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsSitemap { get; set; }
    public IReadOnlyList<StaticSitemapLinkViewModel> SitemapLinks { get; set; } = [];
}

public record StaticSitemapLinkViewModel(string Text, string Href);

public class CookiePolicyViewModel
{
    public string Body { get; set; } = string.Empty;
    public bool AnalyticsAccepted { get; set; }
    public string? Notice { get; set; }
}

public class CookieSettingsSubmitModel
{
    public string? TrackAnalytics { get; set; }
    public string? RequestPath { get; set; }
    public string? SettingsUpdated { get; set; }
}
