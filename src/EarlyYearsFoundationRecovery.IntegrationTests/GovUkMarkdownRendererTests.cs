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

    [Fact]
    public void Render_converts_safe_button_markup()
    {
        var html = new GovUkMarkdownRenderer().Render("{button}[Continue & review](/registration/check?step=1&edit=true){/button}");

        Assert.Contains("class=\"govuk-link govuk-button\"", html);
        Assert.Contains("href=\"/registration/check?step=1&amp;edit=true\"", html);
        Assert.Contains("Continue &amp; review", html);
    }

    [Theory]
    [InlineData("https://example.test/read?one=1&two=2")]
    [InlineData("http://example.test/read")]
    public void Render_converts_safe_external_markup(string destination)
    {
        var html = new GovUkMarkdownRenderer().Render($"{{external}}[Read <more>]({destination}){{/external}}");

        Assert.Contains("class=\"govuk-link\"", html);
        Assert.Contains("target=\"_blank\"", html);
        Assert.Contains("rel=\"noopener noreferrer\"", html);
        Assert.Contains("Read &lt;more&gt; (opens in a new tab)", html);
    }

    [Theory]
    [InlineData("https://example.test/path")]
    [InlineData("//example.test/path")]
    [InlineData("relative/path")]
    [InlineData("/\\example.test/path")]
    public void Render_leaves_unsafe_button_destinations_as_inert_text(string destination)
    {
        var html = new GovUkMarkdownRenderer().Render($"{{button}}[Continue]({destination}){{/button}}");

        Assert.DoesNotContain("<a", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{button}[Continue]", html);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:test@example.test")]
    [InlineData("/local")]
    [InlineData("//example.test/path")]
    [InlineData("https://user:password@example.test/path")]
    public void Render_leaves_unsafe_external_destinations_as_inert_text(string destination)
    {
        var html = new GovUkMarkdownRenderer().Render($"{{external}}[Read]({destination}){{/external}}");

        Assert.DoesNotContain("<a", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{external}[Read]", html);
    }

    [Theory]
    [InlineData("{button}[Missing close](/path)")]
    [InlineData("{button}not a markdown link{/button}")]
    [InlineData("{external}[Nested [label]](https://example.test){/external}")]
    public void Render_leaves_malformed_custom_markup_harmless(string markdown)
    {
        var html = new GovUkMarkdownRenderer().Render(markdown);

        Assert.DoesNotContain("<a", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_sanitizes_generated_custom_markup_as_a_final_boundary()
    {
        var html = new GovUkMarkdownRenderer().Render(
            "{external}[<img src=x onerror=alert(1)>](https://example.test/read){/external}<script>alert('unsafe')</script>");

        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert('unsafe')", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("info", "prompt", "fa-info", "In your setting")]
    [InlineData("brain", "prompt prompt-bg", "fa-brain", "Reflection point")]
    [InlineData("book", "prompt", "fa-book", "Further reading")]
    public void Render_converts_learning_prompt_markup(
        string tag,
        string expectedPromptClass,
        string expectedIconClass,
        string expectedHeading)
    {
        var markdown = "{" + tag + "}\n"
            + "**Safe** body <script>alert('unsafe')</script>\n"
            + "{/" + tag + "}";
        var html = new GovUkMarkdownRenderer().Render(markdown);

        Assert.Contains($"class=\"{expectedPromptClass}\"", html);
        Assert.Contains(expectedIconClass, html);
        Assert.Contains($">{expectedHeading}</h2>", html);
        Assert.Contains("<strong>Safe</strong>", html);
        Assert.DoesNotContain("script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_converts_quote_markup_and_escapes_the_citation()
    {
        var html = new GovUkMarkdownRenderer().Render("""
            {quote}
            Life is **trying** things.

            Ray <script>Bradbury</script>
            {/quote}
            """);

        Assert.Contains("class=\"blockquote-container\"", html);
        Assert.Contains("<blockquote class=\"quote\">", html);
        Assert.Contains("Life is <strong>trying</strong> things.", html);
        Assert.Contains("<cite>Ray &lt;script&gt;Bradbury&lt;/script&gt;</cite>", html);
    }

    [Fact]
    public void Render_converts_two_thirds_markup_using_the_last_line_as_the_right_column()
    {
        var html = new GovUkMarkdownRenderer().Render("""
            {two_thirds}
            Description with **emphasis**.

            ![image title](/path/to/image)
            {/two_thirds}
            """);

        Assert.Contains("class=\"govuk-grid-row\"", html);
        Assert.Contains("class=\"govuk-grid-column-two-thirds\"", html);
        Assert.Contains("Description with <strong>emphasis</strong>.", html);
        Assert.Contains("class=\"govuk-grid-column-one-third\"", html);
        Assert.Contains("<img src=\"/path/to/image\" alt=\"image title\">", html);
    }

    [Theory]
    [InlineData("{info}{/info}")]
    [InlineData("{quote}Only one line{/quote}")]
    [InlineData("{two_thirds}Only one line{/two_thirds}")]
    [InlineData("{brain}{brain}nested{/brain}{/brain}")]
    public void Render_leaves_malformed_block_markup_inert(string markdown)
    {
        var html = new GovUkMarkdownRenderer().Render(markdown);

        Assert.DoesNotContain("class=\"prompt", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("class=\"blockquote-container", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("class=\"govuk-grid-row", html, StringComparison.OrdinalIgnoreCase);
    }
}
