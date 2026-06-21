using EarlyYearsFoundationRecovery.Web.Services;

namespace EarlyYearsFoundationRecovery.IntegrationTests;

public class GovUkMarkdownRendererTests
{
    [Fact]
    public void Render_adds_govuk_typography_classes_to_markdown_html()
    {
        var renderer = new GovUkMarkdownRenderer();

        var html = renderer.Render("""
            ## Heading

            Intro with [a link](/test).

            - One
            - Two

            | Name | Value |
            | --- | --- |
            | Alpha | Beta |
            """);

        Assert.Contains("<h2 class=\"govuk-heading-l\">Heading</h2>", html);
        Assert.Contains("<p class=\"govuk-body\">Intro with <a class=\"govuk-link\" href=\"/test\">a link</a>.</p>", html);
        Assert.Contains("<ul class=\"govuk-list govuk-list--bullet\">", html);
        Assert.Contains("<table class=\"govuk-table\">", html);
        Assert.Contains("<th class=\"govuk-table__header\">Name</th>", html);
        Assert.Contains("<td class=\"govuk-table__cell\">Alpha</td>", html);
    }

    [Fact]
    public void Render_preserves_existing_classes_when_adding_govuk_classes()
    {
        var renderer = new GovUkMarkdownRenderer();

        var html = renderer.Render("<p class=\"custom-class\">Body</p>");

        Assert.Contains("<p class=\"custom-class govuk-body\">Body</p>", html);
    }
}
