using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace EarlyYearsFoundationRecovery.Web.Formatting;

/// <summary>
/// Renders note text with the paragraph and line-break semantics of Rails
/// simple_format while keeping learner content HTML-encoded.
/// </summary>
public static class LearningLogNoteFormatter
{
    public static string ToSimpleFormat(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var paragraphs = Regex.Split(normalized, "\n{2,}");
        var output = new StringBuilder();

        for (var index = 0; index < paragraphs.Length; index++)
        {
            if (index > 0)
            {
                output.Append("\n\n");
            }

            output.Append("<p class=\"govuk-body learning-log-entry\" data-clarity-mask=\"True\">");
            output.Append(WebUtility.HtmlEncode(paragraphs[index]).Replace("\n", "<br />\n", StringComparison.Ordinal));
            output.Append("</p>");
        }

        return output.ToString();
    }
}
