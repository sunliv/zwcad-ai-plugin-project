using System;
using System.Collections.Generic;
using System.Linq;
using ZwcadAi.Core;

namespace ZwcadAi.AiService;

public interface IAiModelClient
{
    string CreateDrawingSpec(AiDrawingSpecRequest request, AiModelCallOptions options);

    string RepairDrawingSpec(AiDrawingSpecRepairRequest request, AiModelCallOptions options);
}

public sealed class AiModelCallOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    public int MaxRetries { get; set; } = 1;

    public string ServiceEndpoint { get; set; } = string.Empty;

    public string ApiKeyEnvironmentVariable { get; set; } = string.Empty;

    public bool LogSensitiveDrawingContent { get; set; }
}

public sealed class LocalAiServiceOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    public int MaxRetries { get; set; } = 1;

    public string ServiceEndpoint { get; set; } = string.Empty;

    public string ApiKeyEnvironmentVariable { get; set; } = string.Empty;

    public bool LogSensitiveDrawingContent { get; set; }

    internal AiModelCallOptions ToCallOptions()
    {
        return new AiModelCallOptions
        {
            Timeout = Timeout,
            MaxRetries = Math.Max(0, MaxRetries),
            ServiceEndpoint = ServiceEndpoint ?? string.Empty,
            ApiKeyEnvironmentVariable = ApiKeyEnvironmentVariable ?? string.Empty,
            LogSensitiveDrawingContent = LogSensitiveDrawingContent
        };
    }
}

public sealed class LocalAiDrawingSpecAdapter : IAiDrawingSpecService
{
    private readonly IAiModelClient _modelClient;
    private readonly LocalAiServiceOptions _options;

    public LocalAiDrawingSpecAdapter(IAiModelClient modelClient)
        : this(modelClient, new LocalAiServiceOptions())
    {
    }

    public LocalAiDrawingSpecAdapter(IAiModelClient modelClient, LocalAiServiceOptions options)
    {
        _modelClient = modelClient ?? throw new ArgumentNullException(nameof(modelClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public AiDrawingSpecResponse CreateDrawingSpec(AiDrawingSpecRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var rawResponse = ExecuteModelCall(
            options => _modelClient.CreateDrawingSpec(request, options));

        return rawResponse.Success
            ? MapRawModelOutput(rawResponse.RawText)
            : Rejected(rawResponse.Issue);
    }

    public AiDrawingSpecResponse RepairDrawingSpec(AiDrawingSpecRepairRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.RepairAttempt < 1)
        {
            return Rejected(new AiModelIssue(
                AiIssueCodes.InvalidRepairAttempt,
                "$.repairAttempt",
                "Repair attempt must be greater than or equal to 1.",
                ValidationSeverity.Error,
                AiModelIssueSource.Service,
                repairable: false));
        }

        if (request.RepairAttempt > request.MaxRepairAttempts)
        {
            return Rejected(new AiModelIssue(
                AiIssueCodes.RepairAttemptLimitExceeded,
                "$.repairAttempt",
                $"Repair attempt {request.RepairAttempt} exceeds the configured limit of {request.MaxRepairAttempts}.",
                ValidationSeverity.Error,
                AiModelIssueSource.Service,
                repairable: false));
        }

        var rawResponse = ExecuteModelCall(
            options => _modelClient.RepairDrawingSpec(request, options));

        return rawResponse.Success
            ? MapRawModelOutput(rawResponse.RawText)
            : Rejected(rawResponse.Issue);
    }

    private ModelCallResult ExecuteModelCall(Func<AiModelCallOptions, string> call)
    {
        var callOptions = _options.ToCallOptions();
        var attempts = Math.Max(1, callOptions.MaxRetries + 1);
        Exception? lastFailure = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                return ModelCallResult.FromRawText(call(callOptions));
            }
            catch (TimeoutException exception)
            {
                lastFailure = exception;
            }
            catch (Exception exception)
            {
                lastFailure = exception;
                break;
            }
        }

        if (lastFailure is TimeoutException)
        {
            return ModelCallResult.FromIssue(new AiModelIssue(
                AiIssueCodes.ModelServiceTimeout,
                "$.service",
                "Model service timed out before returning DrawingSpec JSON.",
                ValidationSeverity.Error,
                AiModelIssueSource.Service,
                repairable: false));
        }

        return ModelCallResult.FromIssue(new AiModelIssue(
            AiIssueCodes.ModelServiceFailed,
            "$.service",
            lastFailure == null
                ? "Model service failed before returning DrawingSpec JSON."
                : $"Model service failed before returning DrawingSpec JSON: {lastFailure.Message}",
            ValidationSeverity.Error,
            AiModelIssueSource.Service,
            repairable: false));
    }

    private static AiDrawingSpecResponse MapRawModelOutput(string rawOutput)
    {
        if (!IsJsonOnlyObject(rawOutput))
        {
            if (ContainsUnsafeCadCommand(rawOutput))
            {
                return Rejected(new AiModelIssue(
                    AiIssueCodes.UnsafeCadCommand,
                    "$",
                    "Model response contains a CAD command or executable script instead of DrawingSpec JSON.",
                    ValidationSeverity.Error,
                    AiModelIssueSource.ModelResponse,
                    repairable: false));
            }

            return Rejected(new AiModelIssue(
                AiIssueCodes.ModelResponseNotJson,
                "$",
                "Model response must be a JSON-only DrawingSpec root object.",
                ValidationSeverity.Error,
                AiModelIssueSource.ModelResponse,
                repairable: false));
        }

        if (!DrawingSpecValidator.TryReadSchemaJson(rawOutput, out var spec, out var schemaValidation))
        {
            return Rejected(
                schemaValidation,
                schemaValidation.Issues.Select(issue => AiModelIssue.FromValidationIssue(
                    issue,
                    AiModelIssueSource.SchemaValidation,
                    ModelPromptContract.IsRepairableIssueCode(issue.Code))),
                rawOutput,
                spec: null);
        }

        var clarifications = spec?.Clarifications ?? Array.Empty<string>();
        if (clarifications.Count > 0)
        {
            return new AiDrawingSpecResponse
            {
                Kind = AiDrawingSpecResponseKind.NeedsClarification,
                Spec = spec,
                DrawingSpecJson = rawOutput,
                Clarifications = LimitClarifications(clarifications, ModelPromptContract.MaxClarificationQuestions),
                Validation = schemaValidation,
                Issues = new[]
                {
                    new AiModelIssue(
                        AiIssueCodes.NeedsClarification,
                        "$.clarifications",
                        "Model response requires user clarification before DrawingSpec can be rendered.",
                        ValidationSeverity.Error,
                        AiModelIssueSource.UserClarification,
                        repairable: false)
                }
            };
        }

        var businessValidation = DrawingSpecValidator.ValidateBusinessRules(spec!);
        if (!businessValidation.IsValid)
        {
            return Rejected(
                businessValidation,
                businessValidation.Issues.Select(issue => AiModelIssue.FromValidationIssue(
                    issue,
                    AiModelIssueSource.BusinessValidation,
                    ModelPromptContract.IsRepairableIssueCode(issue.Code))),
                rawOutput,
                spec);
        }

        return new AiDrawingSpecResponse
        {
            Kind = AiDrawingSpecResponseKind.DrawingSpec,
            Spec = spec,
            DrawingSpecJson = rawOutput,
            Clarifications = Array.Empty<string>(),
            Validation = businessValidation,
            Issues = Array.Empty<AiModelIssue>()
        };
    }

    private static bool IsJsonOnlyObject(string rawOutput)
    {
        var trimmed = (rawOutput ?? string.Empty).Trim();
        return trimmed.Length >= 2
            && trimmed[0] == '{'
            && trimmed[trimmed.Length - 1] == '}';
    }

    private static bool ContainsUnsafeCadCommand(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            return false;
        }

        var text = rawOutput.Trim();
        var unsafeTokens = new[]
        {
            "(command",
            "acedcommand",
            "sendcommand",
            ".scr",
            "_.line",
            "_line",
            "netload",
            "lisp",
            "shell",
            "powershell",
            "cmd.exe"
        };

        return unsafeTokens.Any(token => text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static IReadOnlyList<string> LimitClarifications(IReadOnlyList<string> clarifications, int limit)
    {
        return clarifications
            .Where(question => !string.IsNullOrWhiteSpace(question))
            .Take(Math.Max(0, limit))
            .ToArray();
    }

    private static AiDrawingSpecResponse Rejected(AiModelIssue issue)
    {
        return new AiDrawingSpecResponse
        {
            Kind = AiDrawingSpecResponseKind.Rejected,
            Validation = ValidationResult.Failure(new[]
            {
                new ValidationIssue(issue.Code, issue.Path, issue.Message, issue.Severity)
            }),
            Issues = new[] { issue }
        };
    }

    private static AiDrawingSpecResponse Rejected(
        ValidationResult validation,
        IEnumerable<AiModelIssue> issues,
        string drawingSpecJson,
        DrawingSpec? spec)
    {
        return new AiDrawingSpecResponse
        {
            Kind = AiDrawingSpecResponseKind.Rejected,
            Spec = spec,
            DrawingSpecJson = drawingSpecJson,
            Validation = validation,
            Issues = issues.ToArray()
        };
    }

    private sealed class ModelCallResult
    {
        private ModelCallResult(bool success, string rawText, AiModelIssue issue)
        {
            Success = success;
            RawText = rawText;
            Issue = issue;
        }

        public bool Success { get; }

        public string RawText { get; }

        public AiModelIssue Issue { get; }

        public static ModelCallResult FromRawText(string rawText)
        {
            return new ModelCallResult(true, rawText ?? string.Empty, EmptyIssue());
        }

        public static ModelCallResult FromIssue(AiModelIssue issue)
        {
            return new ModelCallResult(false, string.Empty, issue);
        }

        private static AiModelIssue EmptyIssue()
        {
            return new AiModelIssue(
                AiIssueCodes.ModelServiceFailed,
                "$.service",
                string.Empty,
                ValidationSeverity.Error,
                AiModelIssueSource.Service,
                repairable: false);
        }
    }
}
