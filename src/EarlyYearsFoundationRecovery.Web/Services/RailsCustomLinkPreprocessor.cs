using System.Net;
using System.Text;
using System.Text.Encodings.Web;

namespace EarlyYearsFoundationRecovery.Web.Services;

internal static class RailsCustomLinkPreprocessor
{
    // Contentful custom markup is editorial content. Keep a firm bound so a malformed
    // block cannot make preprocessing consume an arbitrarily large region.
    private const int MaximumConstructLength = 4096;

    public static string Process(string markdown)
    {
        var output = new StringBuilder(markdown.Length);
        var position = 0;

        while (position < markdown.Length)
        {
            var button = markdown.IndexOf("{button}", position, StringComparison.Ordinal);
            var external = markdown.IndexOf("{external}", position, StringComparison.Ordinal);
            var opening = Earliest(button, external);

            if (opening < 0)
            {
                output.Append(markdown, position, markdown.Length - position);
                break;
            }

            output.Append(markdown, position, opening - position);
            var isButton = opening == button;
            var openingTag = isButton ? "{button}" : "{external}";
            var closingTag = isButton ? "{/button}" : "{/external}";
            var contentStart = opening + openingTag.Length;
            var closing = markdown.IndexOf(closingTag, contentStart, StringComparison.Ordinal);

            if (closing < 0)
            {
                AppendInert(output, markdown.AsSpan(opening));
                break;
            }

            if (closing + closingTag.Length - opening > MaximumConstructLength)
            {
                var oversizedEnd = closing + closingTag.Length;
                AppendInert(output, markdown.AsSpan(opening, oversizedEnd - opening));
                position = oversizedEnd;
                continue;
            }

            var constructEnd = closing + closingTag.Length;
            var content = markdown.AsSpan(contentStart, closing - contentStart);
            if (TryParseMarkdownLink(content, out var label, out var destination)
                && (isButton ? IsSafeButtonDestination(destination) : IsSafeExternalDestination(destination)))
            {
                output.Append("<a href=\"");
                output.Append(HtmlEncoder.Default.Encode(destination));
                output.Append(isButton
                    ? "\" class=\"govuk-link govuk-button\">"
                    : "\" class=\"govuk-link\" target=\"_blank\" rel=\"noopener noreferrer\">");
                output.Append(HtmlEncoder.Default.Encode(label));
                if (!isButton)
                {
                    output.Append(" (opens in a new tab)");
                }

                output.Append("</a>");
            }
            else
            {
                // Encode the complete construct so unsafe inner Markdown remains inert text.
                AppendInert(output, markdown.AsSpan(opening, constructEnd - opening));
            }

            position = constructEnd;
        }

        return output.ToString();
    }

    private static int Earliest(int first, int second)
    {
        if (first < 0)
        {
            return second;
        }

        return second < 0 ? first : Math.Min(first, second);
    }

    private static void AppendInert(StringBuilder output, ReadOnlySpan<char> construct)
    {
        // Escape Markdown punctuation before encoding it. Markdig can parse Markdown
        // inside an inline HTML span, so encoding alone would not make the inner link inert.
        var escaped = new StringBuilder(construct.Length);
        foreach (var character in construct)
        {
            if (character is '\\' or '[' or ']' or '(' or ')')
            {
                escaped.Append('\\');
            }

            escaped.Append(character);
        }

        output.Append("<span>");
        output.Append(WebUtility.HtmlEncode(escaped.ToString()));
        output.Append("</span>");
    }

    private static bool TryParseMarkdownLink(
        ReadOnlySpan<char> content,
        out string label,
        out string destination)
    {
        content = content.Trim();
        label = string.Empty;
        destination = string.Empty;

        if (content.Length < 5 || content[0] != '[' || content[^1] != ')')
        {
            return false;
        }

        var separator = content.IndexOf("](".AsSpan(), StringComparison.Ordinal);
        if (separator <= 1 || separator + 2 >= content.Length - 1)
        {
            return false;
        }

        var labelSpan = content[1..separator];
        var destinationSpan = content[(separator + 2)..^1];
        if (labelSpan.IndexOfAny("\r\n[]".AsSpan()) >= 0
            || destinationSpan.IndexOfAny("\r\n()".AsSpan()) >= 0)
        {
            return false;
        }

        label = labelSpan.ToString();
        destination = destinationSpan.ToString();
        return !string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(destination);
    }

    private static bool IsSafeButtonDestination(string destination)
    {
        if (!destination.StartsWith("/", StringComparison.Ordinal)
            || destination.StartsWith("//", StringComparison.Ordinal)
            || destination.Contains('\\')
            || destination.Any(char.IsControl))
        {
            return false;
        }

        return Uri.TryCreate(destination, UriKind.Relative, out _);
    }

    private static bool IsSafeExternalDestination(string destination)
    {
        return Uri.TryCreate(destination, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrWhiteSpace(uri.Host)
            && string.IsNullOrEmpty(uri.UserInfo);
    }
}
