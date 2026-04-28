using System;
using System.Collections.Generic;
using System.Linq;
using ZwcadAi.Core;

namespace ZwcadAi.AiService;

public interface IAiDrawingSpecService
{
    AiDrawingSpecResponse CreateDrawingSpec(AiDrawingSpecRequest request);

    AiDrawingSpecResponse RepairDrawingSpec(AiDrawingSpecRepairRequest request);
}

public sealed class AiDrawingSpecRequest
{
    public string RequestId { get; set; } = string.Empty;

    public string UserRequest { get; set; } = string.Empty;

    public string PromptVersion { get; set; } = ModelPromptContract.PromptVersion;

    public string Units { get; set; } = "mm";

    public string Domain { get; set; } = DrawingDomain.MechanicalPlate;

    public IReadOnlyList<string> AllowedEntityTypes { get; set; } = ModelPromptContract.AllowedEntityTypes;

    public IReadOnlyList<string> AllowedDimensionTypes { get; set; } = ModelPromptContract.AllowedDimensionTypes;

    public string LayerStandard { get; set; } = ModelPromptContract.LayerStandard;

    public string DrawingSpecVersion { get; set; } = DrawingSpecWireFormat.Version;

    public int MaxClarificationQuestions { get; set; } = ModelPromptContract.MaxClarificationQuestions;
}

public sealed class AiDrawingSpecRepairRequest
{
    public string InvalidDrawingSpecJson { get; set; } = string.Empty;

    public IReadOnlyList<AiModelIssue> Issues { get; set; } = Array.Empty<AiModelIssue>();

    public int RepairAttempt { get; set; } = 1;

    public int MaxRepairAttempts { get; set; } = ModelPromptContract.MaxRepairAttempts;

    public AiRepairStrategy RepairStrategy { get; set; } = AiRepairStrategy.RepairDrawingSpecOnly;
}

public sealed class AiDrawingSpecResponse
{
    public AiDrawingSpecResponseKind Kind { get; set; } = AiDrawingSpecResponseKind.Unknown;

    public DrawingSpec? Spec { get; set; }

    public string DrawingSpecJson { get; set; } = string.Empty;

    public IReadOnlyList<string> Clarifications { get; set; } = Array.Empty<string>();

    public ValidationResult Validation { get; set; } = ValidationResult.Success();

    public IReadOnlyList<AiModelIssue> Issues { get; set; } = Array.Empty<AiModelIssue>();
}

public enum AiDrawingSpecResponseKind
{
    Unknown = 0,
    DrawingSpec = 1,
    NeedsClarification = 2,
    Rejected = 3
}

public enum AiRepairStrategy
{
    RepairDrawingSpecOnly = 0
}

public enum AiModelIssueSource
{
    ModelResponse = 0,
    SchemaValidation = 1,
    BusinessValidation = 2,
    Renderer = 3,
    UserClarification = 4,
    Service = 5
}

public sealed class AiModelIssue
{
    public AiModelIssue()
    {
    }

    public AiModelIssue(
        string code,
        string path,
        string message,
        ValidationSeverity severity,
        AiModelIssueSource source,
        bool repairable)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("AI model issue code is required.", nameof(code));
        }

        Code = code;
        Path = path ?? string.Empty;
        Message = message ?? string.Empty;
        Severity = severity;
        Source = source;
        Repairable = repairable;
    }

    public string Code { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;

    public AiModelIssueSource Source { get; set; } = AiModelIssueSource.ModelResponse;

    public bool Repairable { get; set; }

    public static AiModelIssue FromValidationIssue(
        ValidationIssue issue,
        AiModelIssueSource source,
        bool repairable)
    {
        if (issue == null)
        {
            throw new ArgumentNullException(nameof(issue));
        }

        return new AiModelIssue(issue.Code, issue.Path, issue.Message, issue.Severity, source, repairable);
    }
}

public static class AiIssueCodes
{
    public const string NeedsClarification = "needs_clarification";
    public const string ModelResponseNotJson = "model_response_not_json";
    public const string UnsafeCadCommand = "unsafe_cad_command";
    public const string InvalidRepairAttempt = "invalid_repair_attempt";
    public const string RepairAttemptLimitExceeded = "repair_attempt_limit_exceeded";
    public const string ModelServiceTimeout = "model_service_timeout";
    public const string ModelServiceFailed = "model_service_failed";
}

public static class ModelPromptContract
{
    public const string PromptVersion = "p4-01-model-prompt-contract-v1";
    public const string LayerStandard = "enterprise-default-v1";
    public const int MaxClarificationQuestions = 3;
    public const int MaxRepairAttempts = 2;

    public static IReadOnlyList<string> AllowedEntityTypes { get; } =
        new[]
        {
            EntityTypes.Line,
            EntityTypes.Polyline,
            EntityTypes.Circle,
            EntityTypes.Arc,
            EntityTypes.Text,
            EntityTypes.MText,
            EntityTypes.CenterMark
        };

    public static IReadOnlyList<string> AllowedDimensionTypes { get; } =
        new[]
        {
            DimensionTypes.Linear,
            DimensionTypes.Aligned,
            DimensionTypes.Radius,
            DimensionTypes.Diameter,
            DimensionTypes.Angular
        };

    public static IReadOnlyList<string> RepairableIssueCodes { get; } =
        new[]
        {
            "invalid_json",
            "unknown_property",
            "missing_required",
            "invalid_type",
            "invalid_value",
            "string_too_short",
            "array_too_small",
            "invalid_point2d",
            "unsupported_entity_type",
            "unsupported_dimension_type",
            "unsupported_units",
            "unsupported_layer",
            "missing_required_layer",
            "invalid_layer_color",
            "invalid_layer_linetype",
            "invalid_layer_lineweight",
            "missing_entity_id",
            "duplicate_entity_id",
            "invalid_entity_id",
            "missing_entity_layer",
            "invalid_center_mark_layer",
            "invalid_text_layer",
            "invalid_line_geometry",
            "invalid_polyline_points",
            "invalid_circle_geometry",
            "invalid_arc_geometry",
            "invalid_center_mark_geometry",
            "invalid_text_geometry",
            "point_out_of_range",
            "radius_out_of_range",
            "size_out_of_range",
            "text_height_out_of_range",
            "missing_dimension_id",
            "duplicate_dimension_id",
            "invalid_dimension_id",
            "missing_dimension_layer",
            "invalid_dimension_layer",
            "missing_dimension_geometry",
            "missing_dimension_target"
        };

    public static bool IsRepairableIssueCode(string code)
    {
        return !string.IsNullOrWhiteSpace(code)
            && RepairableIssueCodes.Contains(code, StringComparer.Ordinal);
    }
}
