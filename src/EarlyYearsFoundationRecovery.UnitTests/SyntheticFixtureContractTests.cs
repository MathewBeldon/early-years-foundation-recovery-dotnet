using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EarlyYearsFoundationRecovery.UnitTests;

/// <summary>
/// Rails v1.5.0 commit ac546721: User.find_or_create_from_gov_one always ends in save!.
/// Complete-user validations require first_name, last_name, setting_type_id, and
/// terms_and_conditions_agreed_at on update; User.validate_setting_type_id includes 'other'.
/// </summary>
public sealed class SyntheticFixtureContractTests
{
    private const string RailsCommit = "ac546721";
    private const string ExistingEmail = "existing@example.test";
    private const string NewEmail = "new@example.test";
    private const string ResumingEmail = "resuming@example.test";
    private const string AssessmentEmail = "assessment@example.test";
    private const string OtherSettingTypeId = "other";
    private const string TermsAgreedAtUtc = "2026-01-01T00:00:00Z";

    [Fact]
    public void Complete_fixture_survives_gov_one_save_and_stays_aligned_across_sql_and_json()
    {
        var sql = ExecutableSql(File.ReadAllText(Path.Combine(RepositoryRoot(), "parity", "fixtures", "synthetic-fixtures.sql")));
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "parity", "fixtures", "synthetic-fixtures.json")));

        var insert = ParseInsert(sql, "users");
        var upsertColumns = ParseUpsertColumns(sql);
        var jsonUsers = json.RootElement.GetProperty("users").EnumerateArray().ToArray();

        Assert.Equal(4, insert.Rows.Count);
        Assert.Equal(4, jsonUsers.Length);

        var sqlExisting = insert.Row(ExistingEmail);
        var sqlNew = insert.Row(NewEmail);
        var jsonExisting = JsonUser(jsonUsers, ExistingEmail);
        var jsonNew = JsonUser(jsonUsers, NewEmail);
        var sqlResuming = insert.Row(ResumingEmail);
        var jsonResuming = JsonUser(jsonUsers, ResumingEmail);
        var sqlAssessment = insert.Row(AssessmentEmail);
        var jsonAssessment = JsonUser(jsonUsers, AssessmentEmail);

        Assert.Equal("true", sqlExisting["registration_complete"]);
        Assert.Equal("Synthetic", Unquote(sqlExisting["first_name"]));
        Assert.Equal("Learner", Unquote(sqlExisting["last_name"]));
        Assert.Equal(OtherSettingTypeId, Unquote(sqlExisting["setting_type_id"]));
        Assert.Equal(TermsAgreedAtUtc, NormalizeTimestamp(sqlExisting["terms_and_conditions_agreed_at"]));
        Assert.True(
            jsonExisting.GetProperty("registrationComplete").GetBoolean(),
            $"{RailsCommit} User.find_or_create_from_gov_one save! runs complete-user validations only when registration_complete is true.");
        Assert.Equal(OtherSettingTypeId, jsonExisting.GetProperty("settingTypeId").GetString());
        Assert.Equal(TermsAgreedAtUtc, jsonExisting.GetProperty("termsAndConditionsAgreedAt").GetString());

        Assert.Equal("false", sqlNew["registration_complete"]);
        Assert.Equal("null", sqlNew["first_name"]);
        Assert.Equal("null", sqlNew["last_name"]);
        Assert.Equal("null", sqlNew["setting_type_id"]);
        Assert.Equal("null", sqlNew["terms_and_conditions_agreed_at"]);
        Assert.False(jsonNew.GetProperty("registrationComplete").GetBoolean());
        Assert.True(
            JsonNullOrMissing(jsonNew, "settingTypeId"),
            $"{NewEmail} must stay incomplete: null setting_type_id so User.validate_setting_type_id does not run.");
        Assert.True(
            JsonNullOrMissing(jsonNew, "termsAndConditionsAgreedAt"),
            $"{NewEmail} must stay incomplete: null terms_and_conditions_agreed_at.");

        Assert.Equal(Unquote(sqlExisting["setting_type_id"]), jsonExisting.GetProperty("settingTypeId").GetString());
        Assert.Equal(NormalizeTimestamp(sqlExisting["terms_and_conditions_agreed_at"]), jsonExisting.GetProperty("termsAndConditionsAgreedAt").GetString());
        Assert.True(
            JsonNullOrMissing(jsonNew, "settingTypeId") && sqlNew["setting_type_id"] == "null",
            "JSON and executable SQL drifted on new@example.test setting_type_id.");
        Assert.True(
            JsonNullOrMissing(jsonNew, "termsAndConditionsAgreedAt") && sqlNew["terms_and_conditions_agreed_at"] == "null",
            "JSON and executable SQL drifted on new@example.test terms_and_conditions_agreed_at.");

        Assert.Equal("false", sqlResuming["registration_complete"]);
        Assert.Equal("synthetic-resuming", Unquote(sqlResuming["gov_one_id"]));
        Assert.Equal("England", Unquote(sqlResuming["country"]));
        Assert.Equal("other", Unquote(sqlResuming["setting_type_id"]));
        Assert.Equal("Childminder", Unquote(sqlResuming["setting_type_other"]));
        Assert.Equal("false", sqlResuming["training_emails"]);
        Assert.Equal("null", sqlResuming["research_participant"]);
        Assert.Equal("synthetic-resuming", jsonResuming.GetProperty("govOneId").GetString());
        Assert.Equal("research-participant", jsonResuming.GetProperty("nextRegistrationStep").GetString());
        Assert.Equal(Unquote(sqlResuming["country"]), jsonResuming.GetProperty("country").GetString());
        Assert.Equal(Unquote(sqlResuming["setting_type_other"]), jsonResuming.GetProperty("settingTypeOther").GetString());
        Assert.Equal(sqlResuming["training_emails"] == "true", jsonResuming.GetProperty("trainingEmails").GetBoolean());
        Assert.Equal(JsonValueKind.Null, jsonResuming.GetProperty("researchParticipant").ValueKind);

        Assert.Equal("true", sqlAssessment["registration_complete"]);
        Assert.Equal("synthetic-assessment", Unquote(sqlAssessment["gov_one_id"]));
        Assert.Equal("Assessment", Unquote(sqlAssessment["first_name"]));
        Assert.Equal("Learner", Unquote(sqlAssessment["last_name"]));
        Assert.Equal(OtherSettingTypeId, Unquote(sqlAssessment["setting_type_id"]));
        Assert.Equal(TermsAgreedAtUtc, NormalizeTimestamp(sqlAssessment["terms_and_conditions_agreed_at"]));
        Assert.True(jsonAssessment.GetProperty("registrationComplete").GetBoolean());
        Assert.Equal("synthetic-assessment", jsonAssessment.GetProperty("govOneId").GetString());
        Assert.Equal(OtherSettingTypeId, jsonAssessment.GetProperty("settingTypeId").GetString());
        Assert.Equal(TermsAgreedAtUtc, jsonAssessment.GetProperty("termsAndConditionsAgreedAt").GetString());
        Assert.Equal(Unquote(sqlAssessment["gov_one_id"]), jsonAssessment.GetProperty("govOneId").GetString());
        Assert.Equal(Unquote(sqlAssessment["first_name"]), jsonAssessment.GetProperty("firstName").GetString());
        Assert.Equal(Unquote(sqlAssessment["last_name"]), jsonAssessment.GetProperty("lastName").GetString());

        Assert.Contains("setting_type_id", upsertColumns);
        Assert.Contains("terms_and_conditions_agreed_at", upsertColumns);
        Assert.Contains("country", upsertColumns);
        Assert.Contains("setting_type_other", upsertColumns);
        Assert.Contains("training_emails", upsertColumns);
        Assert.Contains("research_participant", upsertColumns);
    }

    [Fact]
    public void Assessment_fixture_seeds_failed_module_one_and_passed_module_two()
    {
        var sql = ExecutableSql(File.ReadAllText(Path.Combine(RepositoryRoot(), "parity", "fixtures", "synthetic-fixtures.sql")));
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "parity", "fixtures", "synthetic-fixtures.json")));

        var assessments = ParseInsert(sql, "assessments");
        var progress = ParseInsert(sql, "user_module_progress");
        var jsonAssessments = json.RootElement.GetProperty("assessments").EnumerateArray().ToArray();
        var jsonProgress = json.RootElement.GetProperty("moduleProgress").EnumerateArray().ToArray();

        Assert.Equal(2, assessments.Rows.Count);
        Assert.Equal(2, jsonAssessments.Length);
        Assert.Equal(2, progress.Rows.Count);
        Assert.Equal(2, jsonProgress.Length);

        var sqlFailed = assessments.RowByModule("module-1");
        var sqlPassed = assessments.RowByModule("module-2");
        var jsonFailed = JsonNamed(jsonAssessments, "trainingModule", "module-1");
        var jsonPassed = JsonNamed(jsonAssessments, "trainingModule", "module-2");

        Assert.Contains(AssessmentEmail, sqlFailed["user_id"], StringComparison.Ordinal);
        Assert.Contains(AssessmentEmail, sqlPassed["user_id"], StringComparison.Ordinal);
        Assert.Equal("50", sqlFailed["score"]);
        Assert.Equal("false", sqlFailed["passed"]);
        Assert.NotEqual("null", sqlFailed["completed_at"]);
        Assert.Equal("75", sqlPassed["score"]);
        Assert.Equal("true", sqlPassed["passed"]);
        Assert.NotEqual("null", sqlPassed["completed_at"]);

        Assert.Equal(AssessmentEmail, jsonFailed.GetProperty("email").GetString());
        Assert.Equal(50, jsonFailed.GetProperty("score").GetInt32());
        Assert.False(jsonFailed.GetProperty("passed").GetBoolean());
        Assert.Equal(AssessmentEmail, jsonPassed.GetProperty("email").GetString());
        Assert.Equal(75, jsonPassed.GetProperty("score").GetInt32());
        Assert.True(jsonPassed.GetProperty("passed").GetBoolean());

        var sqlFailedProgress = progress.RowByModuleName("module-1");
        var sqlPassedProgress = progress.RowByModuleName("module-2");
        var jsonFailedProgress = JsonNamed(jsonProgress, "moduleName", "module-1");
        var jsonPassedProgress = JsonNamed(jsonProgress, "moduleName", "module-2");

        Assert.Equal("assessment-results", Unquote(sqlFailedProgress["last_page"]));
        Assert.Equal("null", sqlFailedProgress["completed_at"]);
        Assert.Equal("assessment-results", Unquote(sqlPassedProgress["last_page"]));
        Assert.Equal("null", sqlPassedProgress["completed_at"]);
        Assert.False(jsonFailedProgress.GetProperty("completed").GetBoolean());
        Assert.False(jsonPassedProgress.GetProperty("completed").GetBoolean());
        Assert.Equal("assessment-results", jsonFailedProgress.GetProperty("lastPage").GetString());
        Assert.Equal("assessment-results", jsonPassedProgress.GetProperty("lastPage").GetString());

        var failedVisited = VisitedPageKeys(sqlFailedProgress["visited_pages"]);
        var passedVisited = VisitedPageKeys(sqlPassedProgress["visited_pages"]);
        Assert.Equal(JsonStringList(jsonFailedProgress, "visitedPages"), failedVisited);
        Assert.Equal(JsonStringList(jsonPassedProgress, "visitedPages"), passedVisited);
        Assert.Contains("assessment-results", failedVisited);
        Assert.Contains("assessment-intro", failedVisited);
        Assert.DoesNotContain("certificate", failedVisited);
        Assert.Contains("assessment-results", passedVisited);
        Assert.DoesNotContain("certificate", passedVisited);
        Assert.False(
            Regex.IsMatch(sql, @"INSERT\s+INTO\s+responses", RegexOptions.IgnoreCase),
            "This slice seeds graded assessments without questionnaire responses.");
    }

    private static string ExecutableSql(string sql)
    {
        var withoutBlocks = Regex.Replace(sql, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        var lines = withoutBlocks.Split('\n').Select(line =>
        {
            var comment = line.IndexOf("--", StringComparison.Ordinal);
            return comment >= 0 ? line[..comment] : line;
        });
        return string.Join('\n', lines);
    }

    private static InsertStatement ParseInsert(string sql, string table)
    {
        var match = Regex.Match(
            sql,
            $@"INSERT\s+INTO\s+{Regex.Escape(table)}\s*\((.*?)\)\s*VALUES\s*(.*?)\s*(?:ON\s+CONFLICT|;)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"Executable SQL must INSERT INTO {table} ... VALUES ...");

        var columns = SplitTopLevel(match.Groups[1].Value).Select(static column => column.Trim()).ToArray();
        var rows = SplitTuples(match.Groups[2].Value)
            .Select(tuple => columns.Zip(SplitTopLevel(tuple), static (column, value) => (Column: column, Value: value.Trim()))
                .ToDictionary(static pair => pair.Column, static pair => pair.Value, StringComparer.OrdinalIgnoreCase))
            .ToList();
        return new InsertStatement(rows);
    }

    private static HashSet<string> ParseUpsertColumns(string sql)
    {
        var match = Regex.Match(
            sql,
            @"ON\s+CONFLICT\s*\(email\)\s*DO\s+UPDATE\s+SET\s+(.*?);",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        Assert.True(match.Success, "Executable SQL must ON CONFLICT (email) DO UPDATE SET the complete-user columns.");
        return SplitTopLevel(match.Groups[1].Value)
            .Select(static assignment => assignment.Split('=', 2)[0].Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static JsonElement JsonUser(IEnumerable<JsonElement> users, string email) =>
        JsonNamed(users, "email", email);

    private static JsonElement JsonNamed(IEnumerable<JsonElement> items, string name, string value) =>
        items.Single(item => item.GetProperty(name).GetString() == value);

    private static List<string> JsonStringList(JsonElement parent, string name) =>
        parent.GetProperty(name).EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToList();

    private static List<string> VisitedPageKeys(string sqlValue)
    {
        var json = Unquote(Regex.Replace(sqlValue.Trim(), @"::jsonb\s*$", string.Empty, RegexOptions.IgnoreCase));
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().Select(property => property.Name).ToList();
    }

    private static bool JsonNullOrMissing(JsonElement user, string name) =>
        !user.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;

    private static string Unquote(string sqlValue)
    {
        var trimmed = sqlValue.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '\'' && trimmed[^1] == '\''
            ? trimmed[1..^1].Replace("''", "'", StringComparison.Ordinal)
            : trimmed;
    }

    private static string? NormalizeTimestamp(string sqlValue)
    {
        var unquoted = Unquote(sqlValue);
        if (unquoted == "null")
        {
            return null;
        }

        var parsed = DateTimeOffset.Parse(unquoted, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        return parsed.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    private static List<string> SplitTuples(string values)
    {
        var tuples = new List<string>();
        var depth = 0;
        var start = -1;
        var inQuote = false;
        for (var i = 0; i < values.Length; i++)
        {
            var ch = values[i];
            if (ch == '\'')
            {
                inQuote = !inQuote;
            }

            if (inQuote)
            {
                continue;
            }

            if (ch == '(')
            {
                if (depth == 0)
                {
                    start = i + 1;
                }

                depth++;
            }
            else if (ch == ')')
            {
                depth--;
                if (depth == 0 && start >= 0)
                {
                    tuples.Add(values[start..i]);
                    start = -1;
                }
            }
        }

        return tuples;
    }

    private static List<string> SplitTopLevel(string text)
    {
        var items = new List<string>();
        var current = new StringBuilder();
        var inQuote = false;
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\'')
            {
                inQuote = !inQuote;
                current.Append(ch);
                continue;
            }

            if (!inQuote && ch == '(')
            {
                depth++;
                current.Append(ch);
                continue;
            }

            if (!inQuote && ch == ')')
            {
                depth--;
                current.Append(ch);
                continue;
            }

            if (!inQuote && depth == 0 && ch == ',')
            {
                items.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
        {
            items.Add(current.ToString());
        }

        return items;
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (directory.EnumerateFiles("*.slnx").Any())
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root from the test assembly.");
    }

    private sealed record InsertStatement(List<Dictionary<string, string>> Rows)
    {
        public Dictionary<string, string> Row(string email) =>
            Rows.Single(row => Unquote(row["email"]) == email);

        public Dictionary<string, string> RowByModule(string moduleName) =>
            Rows.Single(row => Unquote(row["training_module"]) == moduleName);

        public Dictionary<string, string> RowByModuleName(string moduleName) =>
            Rows.Single(row => Unquote(row["module_name"]) == moduleName);
    }
}
