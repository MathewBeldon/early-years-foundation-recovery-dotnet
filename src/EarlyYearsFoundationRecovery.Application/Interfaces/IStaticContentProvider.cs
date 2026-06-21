namespace EarlyYearsFoundationRecovery.Application.Interfaces;

public interface IStaticContentProvider
{
    Task<IReadOnlyList<StaticPageContent>> GetPagesAsync(CancellationToken cancellationToken = default);
    Task<StaticPageContent?> GetPageByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StaticPageContent>> GetFooterPagesAsync(CancellationToken cancellationToken = default);
}

public sealed record StaticPageContent(
    string Name,
    string Title,
    string Heading,
    string Body,
    bool Footer = false,
    bool RequiresAuth = false);
