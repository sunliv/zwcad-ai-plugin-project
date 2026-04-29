using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ZwcadAi.AiService;

public interface IAiCallLogWriter
{
    void Write(AiCallLogEvent logEvent);
}

public sealed class AiCallLogEvent
{
    public string RequestId { get; set; } = string.Empty;

    public string PromptVersion { get; set; } = ModelPromptContract.PromptVersion;

    public AiDrawingSpecResponseKind ResponseKind { get; set; } = AiDrawingSpecResponseKind.Unknown;

    public IReadOnlyList<AiCallLogIssue> Issues { get; set; } = Array.Empty<AiCallLogIssue>();

    public TimeSpan Elapsed { get; set; } = TimeSpan.Zero;

    public int AttemptCount { get; set; }

    public int ClarificationQuestionCount { get; set; }

    public int ClarificationAnswerCount { get; set; }

    public AiCallSensitiveContent? SensitiveContent { get; set; }

    public IReadOnlyList<string> ApiKeyRedactionValues { get; set; } = Array.Empty<string>();
}

public sealed class AiCallLogIssue
{
    public string Code { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public AiModelIssueSource Source { get; set; } = AiModelIssueSource.ModelResponse;

    public static AiCallLogIssue FromModelIssue(AiModelIssue issue)
    {
        if (issue == null)
        {
            throw new ArgumentNullException(nameof(issue));
        }

        return new AiCallLogIssue
        {
            Code = issue.Code ?? string.Empty,
            Path = issue.Path ?? string.Empty,
            Source = issue.Source
        };
    }
}

public sealed class AiCallSensitiveContent
{
    public string UserRequest { get; set; } = string.Empty;

    public string DrawingSpecJson { get; set; } = string.Empty;

    public string InvalidDrawingSpecJson { get; set; } = string.Empty;
}

public sealed class NullAiCallLogWriter : IAiCallLogWriter
{
    public static NullAiCallLogWriter Instance { get; } = new NullAiCallLogWriter();

    private NullAiCallLogWriter()
    {
    }

    public void Write(AiCallLogEvent logEvent)
    {
    }
}

public sealed class RedactedAiCallLogWriter : IAiCallLogWriter
{
    private const string ApiKeyRedactionMarker = "[redacted-api-key]";
    private static readonly Regex ApiKeyPattern = new Regex(
        @"\bsk-[A-Za-z0-9_-]{16,}\b",
        RegexOptions.Compiled);

    private readonly TextWriter _writer;

    public RedactedAiCallLogWriter(TextWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public void Write(AiCallLogEvent logEvent)
    {
        if (logEvent == null)
        {
            throw new ArgumentNullException(nameof(logEvent));
        }

        var builder = new StringBuilder();
        builder.Append('{');
        AppendStringProperty(builder, "requestId", logEvent.RequestId);
        AppendComma(builder);
        AppendStringProperty(builder, "promptVersion", logEvent.PromptVersion);
        AppendComma(builder);
        AppendStringProperty(builder, "responseKind", logEvent.ResponseKind.ToString());
        AppendComma(builder);
        AppendIssueArrayProperty(builder, "issues", logEvent.Issues);
        AppendComma(builder);
        AppendNumberProperty(builder, "elapsedMilliseconds", Math.Max(0d, logEvent.Elapsed.TotalMilliseconds));
        AppendComma(builder);
        AppendIntProperty(builder, "attemptCount", Math.Max(0, logEvent.AttemptCount));
        AppendComma(builder);
        AppendIntProperty(builder, "clarificationQuestionCount", Math.Max(0, logEvent.ClarificationQuestionCount));
        AppendComma(builder);
        AppendIntProperty(builder, "clarificationAnswerCount", Math.Max(0, logEvent.ClarificationAnswerCount));

        if (logEvent.SensitiveContent != null)
        {
            AppendComma(builder);
            AppendSensitiveContentProperty(
                builder,
                "sensitiveContent",
                logEvent.SensitiveContent,
                logEvent.ApiKeyRedactionValues);
        }

        builder.Append('}');
        _writer.WriteLine(builder.ToString());
    }

    private static void AppendIssueArrayProperty(
        StringBuilder builder,
        string propertyName,
        IReadOnlyList<AiCallLogIssue> issues)
    {
        AppendPropertyName(builder, propertyName);
        builder.Append('[');
        var values = issues ?? Array.Empty<AiCallLogIssue>();
        for (var index = 0; index < values.Count; index++)
        {
            if (index > 0)
            {
                AppendComma(builder);
            }

            var issue = values[index];
            builder.Append('{');
            AppendStringProperty(builder, "code", issue.Code);
            AppendComma(builder);
            AppendStringProperty(builder, "path", issue.Path);
            AppendComma(builder);
            AppendStringProperty(builder, "source", issue.Source.ToString());
            builder.Append('}');
        }

        builder.Append(']');
    }

    private static void AppendSensitiveContentProperty(
        StringBuilder builder,
        string propertyName,
        AiCallSensitiveContent sensitiveContent,
        IReadOnlyList<string> apiKeyRedactionValues)
    {
        AppendPropertyName(builder, propertyName);
        builder.Append('{');
        AppendStringProperty(builder, "userRequest", RedactApiKeys(sensitiveContent.UserRequest, apiKeyRedactionValues));
        AppendComma(builder);
        AppendStringProperty(builder, "drawingSpecJson", RedactApiKeys(sensitiveContent.DrawingSpecJson, apiKeyRedactionValues));
        AppendComma(builder);
        AppendStringProperty(builder, "invalidDrawingSpecJson", RedactApiKeys(sensitiveContent.InvalidDrawingSpecJson, apiKeyRedactionValues));
        builder.Append('}');
    }

    private static string RedactApiKeys(string value, IReadOnlyList<string> apiKeyRedactionValues)
    {
        var redacted = value ?? string.Empty;
        foreach (var apiKey in apiKeyRedactionValues ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                continue;
            }

            redacted = redacted.Replace(apiKey, ApiKeyRedactionMarker);
        }

        return ApiKeyPattern.Replace(redacted, ApiKeyRedactionMarker);
    }

    private static void AppendStringProperty(StringBuilder builder, string propertyName, string value)
    {
        AppendPropertyName(builder, propertyName);
        AppendJsonString(builder, value);
    }

    private static void AppendIntProperty(StringBuilder builder, string propertyName, int value)
    {
        AppendPropertyName(builder, propertyName);
        builder.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendNumberProperty(StringBuilder builder, string propertyName, double value)
    {
        AppendPropertyName(builder, propertyName);
        builder.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
    }

    private static void AppendPropertyName(StringBuilder builder, string propertyName)
    {
        AppendJsonString(builder, propertyName);
        builder.Append(':');
    }

    private static void AppendComma(StringBuilder builder)
    {
        builder.Append(',');
    }

    private static void AppendJsonString(StringBuilder builder, string value)
    {
        builder.Append('"');
        var text = value ?? string.Empty;
        foreach (var character in text)
        {
            switch (character)
            {
                case '\\':
                    builder.Append(@"\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\b':
                    builder.Append(@"\b");
                    break;
                case '\f':
                    builder.Append(@"\f");
                    break;
                case '\n':
                    builder.Append(@"\n");
                    break;
                case '\r':
                    builder.Append(@"\r");
                    break;
                case '\t':
                    builder.Append(@"\t");
                    break;
                default:
                    if (char.IsControl(character))
                    {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        builder.Append('"');
    }
}
