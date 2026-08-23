using System.Net;
using System.Text;
using System.Text.Encodings.Web;

namespace EarlyYearsFoundationRecovery.Web.Services;

internal static class RailsCustomLinkPreprocessor
{
    // Contentful custom markup is editorial content. Keep a firm bound so a malformed
    // block cannot make preprocessing consume an arbitrarily large region.
    private const int MaximumConstructLength = 4096;

    private static readonly CustomBlock[] Blocks =
    [
        new("button", BlockKind.Button),
        new("external", BlockKind.External),
        new("info", BlockKind.Prompt),
        new("brain", BlockKind.Prompt),
        new("book", BlockKind.Prompt),
        new("quote", BlockKind.Quote),
        new("two_thirds", BlockKind.TwoThirds),
    ];

    public static string Process(string markdown, Func<string, string> renderNestedMarkdown)
    {
        var output = new StringBuilder(markdown.Length);
        var position = 0;

        while (position < markdown.Length)
        {
            var block = FindNextBlock(markdown, position, out var opening);

            if (block is null)
            {
                output.Append(markdown, position, markdown.Length - position);
                break;
            }

            output.Append(markdown, position, opening - position);
            var openingTag = $"{{{block.Value.Name}}}";
            var closingTag = $"{{/{block.Value.Name}}}";
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
            if (!TryRenderBlock(output, block.Value, content, renderNestedMarkdown))
            {
                AppendInert(output, markdown.AsSpan(opening, constructEnd - opening));
            }

            position = constructEnd;
        }

        return output.ToString();
    }

    private static CustomBlock? FindNextBlock(string markdown, int position, out int opening)
    {
        opening = -1;
        CustomBlock? match = null;
        foreach (var block in Blocks)
        {
            var candidate = markdown.IndexOf($"{{{block.Name}}}", position, StringComparison.Ordinal);
            if (candidate >= 0 && (opening < 0 || candidate < opening))
            {
                opening = candidate;
                match = block;
            }
        }

        return match;
    }

    private static bool TryRenderBlock(
        StringBuilder output,
        CustomBlock block,
        ReadOnlySpan<char> content,
        Func<string, string> renderNestedMarkdown)
    {
        if (content.Contains($"{{{block.Name}}}".AsSpan(), StringComparison.Ordinal))
        {
            return false;
        }

        if (block.Kind is BlockKind.Button or BlockKind.External)
        {
            if (!TryParseMarkdownLink(content, out var label, out var destination)
                || (block.Kind == BlockKind.Button
                    ? !IsSafeButtonDestination(destination)
                    : !IsSafeExternalDestination(destination)))
            {
                return false;
            }

            output.Append("<a href=\"");
            output.Append(HtmlEncoder.Default.Encode(destination));
            output.Append(block.Kind == BlockKind.Button
                ? "\" class=\"govuk-link govuk-button\">"
                : "\" class=\"govuk-link\" target=\"_blank\" rel=\"noopener noreferrer\">");
            output.Append(HtmlEncoder.Default.Encode(label));
            if (block.Kind == BlockKind.External)
            {
                output.Append(" (opens in a new tab)");
            }

            output.Append("</a>");
            return true;
        }

        var body = content.ToString().Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        if (block.Kind == BlockKind.Prompt)
        {
            var (heading, iconClass, backgroundClass) = block.Name switch
            {
                "info" => ("In your setting", "fa-info", string.Empty),
                "brain" => ("Reflection point", "fa-brain", " prompt-bg"),
                _ => ("Further reading", "fa-book", string.Empty),
            };
            output.Append($"<div class=\"prompt{backgroundClass}\"><div class=\"govuk-grid-row\"><div class=\"govuk-grid-column-one-quarter\"><i class=\"fa-2x fa-solid {iconClass}\" aria-describedby=\"{block.Name} icon\"></i></div><div class=\"govuk-grid-column-three-quarters\"><h2 class=\"govuk-heading-m\">{heading}</h2>");
            output.Append(renderNestedMarkdown(body));
            output.Append("</div></div></div>");
            return true;
        }

        if (!TrySplitLastLine(body, out var mainContent, out var finalLine))
        {
            return false;
        }

        if (block.Kind == BlockKind.Quote)
        {
            output.Append("<div class=\"blockquote-container\"><blockquote class=\"quote\">");
            output.Append(renderNestedMarkdown(mainContent));
            output.Append("<cite>");
            output.Append(HtmlEncoder.Default.Encode(finalLine));
            output.Append("</cite></blockquote></div>");
            return true;
        }

        output.Append("<div class=\"govuk-grid-row\"><div class=\"govuk-grid-column-two-thirds\">");
        output.Append(renderNestedMarkdown(mainContent));
        output.Append("</div><div class=\"govuk-grid-column-one-third\">");
        output.Append(renderNestedMarkdown(finalLine));
        output.Append("</div></div>");
        return true;
    }

    private static bool TrySplitLastLine(string content, out string mainContent, out string finalLine)
    {
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var finalIndex = lines.Length - 1;
        while (finalIndex >= 0 && string.IsNullOrWhiteSpace(lines[finalIndex]))
        {
            finalIndex--;
        }

        finalLine = finalIndex >= 0 ? lines[finalIndex].Trim() : string.Empty;
        mainContent = finalIndex > 0 ? string.Join('\n', lines[..finalIndex]).Trim() : string.Empty;
        return !string.IsNullOrWhiteSpace(mainContent) && !string.IsNullOrWhiteSpace(finalLine);
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

    private readonly record struct CustomBlock(string Name, BlockKind Kind);

    private enum BlockKind
    {
        Button,
        External,
        Prompt,
        Quote,
        TwoThirds,
    }
}
