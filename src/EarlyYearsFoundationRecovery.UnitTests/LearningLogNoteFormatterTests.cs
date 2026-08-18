using EarlyYearsFoundationRecovery.Web.Formatting;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class LearningLogNoteFormatterTests
{
    [Fact]
    public void ToSimpleFormat_preserves_paragraphs_and_single_line_breaks()
    {
        var html = LearningLogNoteFormatter.ToSimpleFormat("First\nline\n\nSecond");

        Assert.Equal(
            "<p class=\"govuk-body learning-log-entry\" data-clarity-mask=\"True\">First<br />\nline</p>\n\n" +
            "<p class=\"govuk-body learning-log-entry\" data-clarity-mask=\"True\">Second</p>",
            html);
    }

    [Fact]
    public void ToSimpleFormat_encodes_learner_markup_before_returning_safe_html()
    {
        var html = LearningLogNoteFormatter.ToSimpleFormat("<script>alert('xss')</script>");

        Assert.Contains("&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
    }
}
