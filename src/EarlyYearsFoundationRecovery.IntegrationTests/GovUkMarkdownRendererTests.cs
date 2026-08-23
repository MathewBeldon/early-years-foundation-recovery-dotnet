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

    // Contract: pinned Rails v1.5.0 ContentHelper#m and #sanitize_markdown.
    // See parity/.rails-source/app/helpers/content_helper.rb and
    // parity/.rails-source/spec/helpers/content_helper_spec.rb.
    [Fact]
    public void Render_preserves_allowed_formatting_links_and_assets()
    {
        var renderer = new GovUkMarkdownRenderer();

        var html = renderer.Render("""
            ## Heading

            **Strong** and *emphasised* with [relative](/relative),
            [protocol relative](//images.ctfassets.net/space/asset/file.pdf),
            and [HTTPS](https://example.test/file.pdf).

            <img src="//images.ctfassets.net/space/asset/image.jpg" alt="Description" title="Image" class="content-image" aria-describedby="caption" />

            <a href="https://example.test/file.pdf" download="guide.pdf" target="_blank" rel="noopener noreferrer">Download</a>

            <table><tbody><tr><td colspan="2">Value</td></tr></tbody></table>
            """);

        Assert.Contains("<h2 class=\"govuk-heading-l\">Heading</h2>", html);
        Assert.Contains("<strong>Strong</strong>", html);
        Assert.Contains("<em>emphasised</em>", html);
        Assert.Contains("href=\"/relative\"", html);
        Assert.Contains("href=\"//images.ctfassets.net/space/asset/file.pdf\"", html);
        Assert.Contains("href=\"https://example.test/file.pdf\"", html);
        Assert.Contains("src=\"//images.ctfassets.net/space/asset/image.jpg\"", html);
        Assert.Contains("class=\"content-image\"", html);
        Assert.Contains("aria-describedby=\"caption\"", html);
        Assert.Contains("download=\"guide.pdf\"", html);
        Assert.Contains("target=\"_blank\"", html);
        Assert.Contains("rel=\"noopener noreferrer\"", html);
        Assert.Contains("colspan=\"2\"", html);
    }

    [Theory]
    [InlineData("<script>alert('script')</script>", "<script", "alert('script')")]
    [InlineData("<style>body { display: none }</style>", "<style", "display: none")]
    [InlineData("<iframe src=\"https://example.test\">frame</iframe>", "<iframe", "frame")]
    [InlineData("<object data=\"https://example.test\">object</object>", "<object", "object")]
    public void Render_removes_unsafe_elements_and_their_content(
        string markdown,
        string unsafeElement,
        string unsafeContent)
    {
        var html = new GovUkMarkdownRenderer().Render(markdown);

        Assert.DoesNotContain(unsafeElement, html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(unsafeContent, html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("<img src=\"x\" onerror=\"alert(1)\" style=\"display:none\" data-secret=\"x\">", "onerror")]
    [InlineData("<img src=\"x\" onerror=\"alert(1)\" style=\"display:none\" data-secret=\"x\">", "style=")]
    [InlineData("<img src=\"x\" onerror=\"alert(1)\" style=\"display:none\" data-secret=\"x\">", "data-secret")]
    [InlineData("<a href=\"javascript:alert(1)\">Bad link</a>", "javascript:")]
    [InlineData("<img src=\"data:text/html,alert(1)\" alt=\"Bad\">", "data:")]
    [InlineData("<a href=\"vbscript:msgbox(1)\">Bad link</a>", "vbscript:")]
    public void Render_removes_unsafe_attributes_and_url_schemes(string markdown, string unsafeText)
    {
        var html = new GovUkMarkdownRenderer().Render(markdown);

        Assert.DoesNotContain(unsafeText, html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("http://example.test/file")]
    [InlineData("https://example.test/file")]
    public void Render_preserves_allowed_absolute_url_schemes(string url)
    {
        var html = new GovUkMarkdownRenderer().Render($"[Allowed]({url})");

        Assert.Contains($"href=\"{url}\"", html);
    }
}
