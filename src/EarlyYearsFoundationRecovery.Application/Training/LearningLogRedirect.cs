namespace EarlyYearsFoundationRecovery.Application.Training;

/// <summary>
/// Resolves the post-save location for learning-log notes.
/// Rails v1.5.0 ac546721 Training::NotesController#next_page_path never follows an
/// arbitrary URL: it uses next_page_module/name, then the current page, then
/// user_notes_path. The .NET form also posts NextPageUrl; only a local application
/// path is honoured so that field cannot open-redirect.
/// </summary>
public static class LearningLogRedirect
{
    public const string LearningLogPath = "/my-account/learning-log";

    public static string ResolveNextPagePath(
        string? nextPageUrl,
        string? nextPageModule,
        string? nextPageName,
        string? trainingModule,
        string? pageName)
    {
        if (IsLocalApplicationPath(nextPageUrl))
        {
            return nextPageUrl!;
        }

        // Rails v1.5.0 ac546721 Training::NotesController#next_page_path:61-70.
        if (!string.IsNullOrWhiteSpace(nextPageModule) && !string.IsNullOrWhiteSpace(nextPageName))
        {
            return ContentPagePath(nextPageModule, nextPageName);
        }

        if (!string.IsNullOrWhiteSpace(trainingModule) && !string.IsNullOrWhiteSpace(pageName))
        {
            return ContentPagePath(trainingModule, pageName);
        }

        return LearningLogPath;
    }

    public static bool IsLocalApplicationPath(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        // Root-relative application paths only. Reject schemes, protocol-relative
        // URLs ("//evil.example"), and the "/\" open-redirect form.
        if (url[0] != '/')
        {
            return false;
        }

        if (url.Length > 1 && (url[1] is '/' or '\\'))
        {
            return false;
        }

        foreach (var ch in url)
        {
            if (char.IsControl(ch) || ch == '\\')
            {
                return false;
            }
        }

        return true;
    }

    private static string ContentPagePath(string moduleName, string pageName) =>
        $"/modules/{moduleName}/content-pages/{pageName}";
}
