using System.Text.RegularExpressions;
using Markdig;

namespace EarlyYearsFoundationRecovery.Web.Services;

public sealed partial class GovUkMarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .Build();

    public string Render(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var html = Markdown.ToHtml(markdown, Pipeline);
        return ApplyGovUkClasses(html).Trim();
    }

    private static string ApplyGovUkClasses(string html)
    {
        html = AddClasses(html, "h1", "govuk-heading-xl");
        html = AddClasses(html, "h2", "govuk-heading-l");
        html = AddClasses(html, "h3", "govuk-heading-m");
        html = AddClasses(html, "h4", "govuk-heading-s");
        html = AddClasses(html, "h5", "govuk-heading-s");
        html = AddClasses(html, "h6", "govuk-heading-s");
        html = AddClasses(html, "p", "govuk-body");
        html = AddClasses(html, "a", "govuk-link");
        html = AddClasses(html, "ul", "govuk-list govuk-list--bullet");
        html = AddClasses(html, "ol", "govuk-list govuk-list--number");
        html = AddClasses(html, "table", "govuk-table");
        html = AddClasses(html, "caption", "govuk-table__caption");
        html = AddClasses(html, "th", "govuk-table__header");
        html = AddClasses(html, "td", "govuk-table__cell");
        html = AddClasses(html, "hr", "govuk-section-break govuk-section-break--m govuk-section-break--visible");

        return html
            .Replace("<blockquote>", "<div class=\"govuk-inset-text\">", StringComparison.OrdinalIgnoreCase)
            .Replace("</blockquote>", "</div>", StringComparison.OrdinalIgnoreCase);
    }

    private static string AddClasses(string html, string tagName, string classes)
    {
        var regex = new Regex($@"<{tagName}(?<attrs>\s[^>]*)?>", RegexOptions.IgnoreCase);
        return regex.Replace(html, match =>
        {
            var attrs = match.Groups["attrs"].Success ? match.Groups["attrs"].Value : string.Empty;
            var classMatch = ClassAttributeRegex().Match(attrs);
            if (!classMatch.Success)
            {
                return $"<{tagName} class=\"{classes}\"{attrs}>";
            }

            var existingClasses = classMatch.Groups["classes"].Value;
            var mergedClasses = MergeClasses(existingClasses, classes);
            var updatedAttrs = ClassAttributeRegex().Replace(attrs, $"class=\"{mergedClasses}\"", 1);

            return $"<{tagName}{updatedAttrs}>";
        });
    }

    private static string MergeClasses(string existingClasses, string classes)
    {
        var merged = existingClasses
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        foreach (var className in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!merged.Contains(className, StringComparer.Ordinal))
            {
                merged.Add(className);
            }
        }

        return string.Join(' ', merged);
    }

    [GeneratedRegex("class\\s*=\\s*\"(?<classes>[^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex ClassAttributeRegex();
}
