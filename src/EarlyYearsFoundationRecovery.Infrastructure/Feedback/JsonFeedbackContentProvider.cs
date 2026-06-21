using System.Text.Json;
using System.Text.Json.Serialization;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EarlyYearsFoundationRecovery.Infrastructure.Feedback;

public sealed class JsonFeedbackContentProvider(IHostEnvironment environment, ILogger<JsonFeedbackContentProvider> logger)
    : IFeedbackContentProvider
{
    private FeedbackFormContent? _form;

    private FeedbackFormContent Form => _form ??= LoadForm();

    public Task<FeedbackFormContent> GetFormAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Form);

    public Task<FeedbackQuestionContent?> GetQuestionAsync(string questionName, CancellationToken cancellationToken = default) =>
        Task.FromResult(Form.PageByName(questionName));

    private FeedbackFormContent LoadForm()
    {
        var path = ResolveContentPath();
        if (!File.Exists(path))
        {
            logger.LogWarning("Demo feedback content not found at {Path}; using empty form.", path);
            return new FeedbackFormContent([]);
        }

        var json = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<FeedbackContentDocument>(json, JsonOptions)
            ?? new FeedbackContentDocument();

        return new FeedbackFormContent(document.Questions.Select(ToQuestion).ToList());
    }

    private string ResolveContentPath()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "data", "demo-feedback-content.json")),
            Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "..", "data", "demo-feedback-content.json")),
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private static FeedbackQuestionContent ToQuestion(QuestionRecord record) => new(
        record.Name,
        record.PageType,
        record.InputType,
        record.Heading,
        record.Legend,
        record.Body,
        record.Options,
        record.Skippable,
        record.HasOther,
        record.OtherLabel,
        record.HasMore,
        record.HasOr,
        record.OrLabel);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class FeedbackContentDocument
    {
        public List<QuestionRecord> Questions { get; init; } = [];
    }

    private sealed record QuestionRecord
    {
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("pageType")]
        public string PageType { get; init; } = string.Empty;

        [JsonPropertyName("inputType")]
        public string InputType { get; init; } = string.Empty;

        public string Heading { get; init; } = string.Empty;
        public string Legend { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public List<string> Options { get; init; } = [];
        public bool Skippable { get; init; }
        public bool HasOther { get; init; }
        public string? OtherLabel { get; init; }
        public bool HasMore { get; init; }
        public bool HasOr { get; init; }
        public string? OrLabel { get; init; }
    }
}
