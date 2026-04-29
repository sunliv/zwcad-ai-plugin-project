using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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

    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
}

public sealed class LocalAiServiceOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    public int MaxRetries { get; set; } = 1;

    public string ServiceEndpoint { get; set; } = string.Empty;

    public string ApiKeyEnvironmentVariable { get; set; } = string.Empty;

    public bool LogSensitiveDrawingContent { get; set; }

    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

    internal AiModelCallOptions ToCallOptions()
    {
        return new AiModelCallOptions
        {
            Timeout = Timeout,
            MaxRetries = Math.Max(0, MaxRetries),
            ServiceEndpoint = ServiceEndpoint ?? string.Empty,
            ApiKeyEnvironmentVariable = ApiKeyEnvironmentVariable ?? string.Empty,
            LogSensitiveDrawingContent = LogSensitiveDrawingContent,
            CancellationToken = CancellationToken
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

        return CreateDrawingSpecCore(request, clarificationState: null);
    }

    public AiDrawingSpecResponse ContinueDrawingSpecAfterClarification(AiClarificationFollowUpRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var validationIssue = ValidateClarificationFollowUpRequest(request);
        if (validationIssue != null)
        {
            return Rejected(validationIssue);
        }

        var state = request.ClarificationState;
        var answers = NormalizeStrings(request.UserAnswers).ToArray();
        var answeredState = new AiDrawingSpecClarificationState
        {
            RequestId = state.RequestId,
            OriginalUserRequest = state.OriginalUserRequest,
            ClarificationQuestions = NormalizeStrings(state.ClarificationQuestions).ToArray(),
            UserAnswers = answers,
            PromptVersion = NormalizePromptVersion(state.PromptVersion)
        };
        var followUpCreateRequest = new AiDrawingSpecRequest
        {
            RequestId = answeredState.RequestId,
            UserRequest = BuildClarifiedUserRequest(answeredState),
            PromptVersion = answeredState.PromptVersion
        };

        return CreateDrawingSpecCore(followUpCreateRequest, answeredState);
    }

    private AiDrawingSpecResponse CreateDrawingSpecCore(
        AiDrawingSpecRequest request,
        AiDrawingSpecClarificationState? clarificationState)
    {
        var rawResponse = ExecuteModelCall(
            options => _modelClient.CreateDrawingSpec(request, options));

        if (!rawResponse.Success)
        {
            var rejectedResponse = Rejected(rawResponse.Issue);
            rejectedResponse.ClarificationState = clarificationState;
            return rejectedResponse;
        }

        var mappedResponse = MapRawModelOutput(rawResponse.RawText);
        var resolvedResponse = RepairUntilResolved(
            mappedResponse,
            nextRepairAttempt: 1,
            maxRepairAttempts: ModelPromptContract.MaxRepairAttempts);
        return AttachClarificationState(resolvedResponse, request, clarificationState);
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

        if (!rawResponse.Success)
        {
            return Rejected(rawResponse.Issue);
        }

        var mappedResponse = MapRawModelOutput(rawResponse.RawText);
        return RepairUntilResolved(
            mappedResponse,
            nextRepairAttempt: request.RepairAttempt + 1,
            maxRepairAttempts: request.MaxRepairAttempts);
    }

    private AiDrawingSpecResponse RepairUntilResolved(
        AiDrawingSpecResponse mappedResponse,
        int nextRepairAttempt,
        int maxRepairAttempts)
    {
        var currentResponse = mappedResponse;
        var repairAttempt = nextRepairAttempt;

        while (CanRepair(currentResponse))
        {
            if (repairAttempt > maxRepairAttempts)
            {
                return RejectedRepairAttemptLimitExceeded(
                    currentResponse,
                    repairAttempt,
                    maxRepairAttempts);
            }

            var repairRequest = new AiDrawingSpecRepairRequest
            {
                InvalidDrawingSpecJson = currentResponse.DrawingSpecJson,
                Issues = currentResponse.Issues,
                RepairAttempt = repairAttempt,
                MaxRepairAttempts = maxRepairAttempts,
                RepairStrategy = AiRepairStrategy.RepairDrawingSpecOnly
            };

            var rawRepairResponse = ExecuteModelCall(
                options => _modelClient.RepairDrawingSpec(repairRequest, options));

            if (!rawRepairResponse.Success)
            {
                return Rejected(rawRepairResponse.Issue);
            }

            currentResponse = MapRawModelOutput(rawRepairResponse.RawText);
            repairAttempt++;
        }

        return currentResponse;
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
            catch (OperationCanceledException exception)
            {
                lastFailure = exception;
                break;
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

        if (lastFailure is OperationCanceledException)
        {
            return ModelCallResult.FromIssue(new AiModelIssue(
                AiIssueCodes.ModelServiceCanceled,
                "$.service",
                "Model service call was canceled before returning DrawingSpec JSON.",
                ValidationSeverity.Error,
                AiModelIssueSource.Service,
                repairable: false));
        }

        return ModelCallResult.FromIssue(new AiModelIssue(
            AiIssueCodes.ModelServiceFailed,
            "$.service",
            "Model service failed before returning DrawingSpec JSON.",
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

    private static AiDrawingSpecResponse AttachClarificationState(
        AiDrawingSpecResponse response,
        AiDrawingSpecRequest request,
        AiDrawingSpecClarificationState? existingState)
    {
        if (response.Kind != AiDrawingSpecResponseKind.NeedsClarification)
        {
            response.ClarificationState = existingState;
            return response;
        }

        var stateRequestId = existingState?.RequestId;
        var stateOriginalUserRequest = existingState?.OriginalUserRequest;
        var statePromptVersion = existingState?.PromptVersion;

        response.ClarificationState = new AiDrawingSpecClarificationState
        {
            RequestId = string.IsNullOrWhiteSpace(stateRequestId)
                ? request.RequestId
                : stateRequestId!,
            OriginalUserRequest = string.IsNullOrWhiteSpace(stateOriginalUserRequest)
                ? request.UserRequest
                : stateOriginalUserRequest!,
            ClarificationQuestions = NormalizeStrings(existingState?.ClarificationQuestions)
                .Concat(NormalizeStrings(response.Clarifications))
                .ToArray(),
            UserAnswers = NormalizeStrings(existingState?.UserAnswers).ToArray(),
            PromptVersion = NormalizePromptVersion(
                string.IsNullOrWhiteSpace(statePromptVersion)
                    ? request.PromptVersion
                    : statePromptVersion)
        };
        return response;
    }

    private static AiModelIssue? ValidateClarificationFollowUpRequest(AiClarificationFollowUpRequest request)
    {
        var state = request.ClarificationState;
        if (state == null
            || string.IsNullOrWhiteSpace(state.RequestId)
            || string.IsNullOrWhiteSpace(state.OriginalUserRequest)
            || string.IsNullOrWhiteSpace(state.PromptVersion)
            || NormalizeStrings(state.ClarificationQuestions).Count == 0)
        {
            return new AiModelIssue(
                AiIssueCodes.InvalidClarificationState,
                "$.clarificationState",
                "Clarification follow-up requires request id, original user request, clarification questions, and prompt version.",
                ValidationSeverity.Error,
                AiModelIssueSource.Service,
                repairable: false);
        }

        if (NormalizeStrings(request.UserAnswers).Count == 0)
        {
            return new AiModelIssue(
                AiIssueCodes.MissingClarificationAnswer,
                "$.userAnswers",
                "Clarification follow-up requires at least one user answer.",
                ValidationSeverity.Error,
                AiModelIssueSource.UserClarification,
                repairable: false);
        }

        return null;
    }

    private static string BuildClarifiedUserRequest(AiDrawingSpecClarificationState state)
    {
        var questions = NormalizeStrings(state.ClarificationQuestions);
        var answers = NormalizeStrings(state.UserAnswers);
        var builder = new StringBuilder();
        builder.AppendLine(state.OriginalUserRequest.Trim());
        builder.AppendLine();
        builder.AppendLine("Clarification follow-up:");

        var itemCount = Math.Max(questions.Count, answers.Count);
        for (var index = 0; index < itemCount; index++)
        {
            if (index < questions.Count)
            {
                builder.Append("Question: ");
                builder.AppendLine(questions[index]);
            }

            if (index < answers.Count)
            {
                builder.Append("Answer: ");
                builder.AppendLine(answers[index]);
            }
        }

        return builder.ToString().Trim();
    }

    private static string NormalizePromptVersion(string? promptVersion)
    {
        if (string.IsNullOrWhiteSpace(promptVersion))
        {
            return ModelPromptContract.PromptVersion;
        }

        return promptVersion!;
    }

    private static IReadOnlyList<string> NormalizeStrings(IReadOnlyList<string>? values)
    {
        return (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();
    }

    private static bool CanRepair(AiDrawingSpecResponse response)
    {
        return response.Kind == AiDrawingSpecResponseKind.Rejected
            && response.Issues.Count > 0
            && response.Issues.All(issue => issue.Repairable);
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

    private static AiDrawingSpecResponse RejectedRepairAttemptLimitExceeded(
        AiDrawingSpecResponse lastResponse,
        int repairAttempt,
        int maxRepairAttempts)
    {
        var issue = new AiModelIssue(
            AiIssueCodes.RepairAttemptLimitExceeded,
            "$.repairAttempt",
            $"Repair attempt {repairAttempt} exceeds the configured limit of {maxRepairAttempts}.",
            ValidationSeverity.Error,
            AiModelIssueSource.Service,
            repairable: false);
        var validationIssue = new ValidationIssue(
            issue.Code,
            issue.Path,
            issue.Message,
            issue.Severity);

        return new AiDrawingSpecResponse
        {
            Kind = AiDrawingSpecResponseKind.Rejected,
            Spec = lastResponse.Spec,
            DrawingSpecJson = lastResponse.DrawingSpecJson,
            Clarifications = lastResponse.Clarifications,
            Validation = ValidationResult.Failure(lastResponse.Validation.Issues.Concat(new[] { validationIssue })),
            Issues = lastResponse.Issues.Concat(new[] { issue }).ToArray()
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
