using System;
using System.Collections.Generic;
using ZwcadAi.Core;

namespace ZwcadAi.AiService;

public interface IAiDrawingSpecService
{
    AiDrawingSpecResponse CreateDrawingSpec(AiDrawingSpecRequest request);
}

public sealed class AiDrawingSpecRequest
{
    public string UserRequest { get; set; } = string.Empty;

    public string Units { get; set; } = "mm";

    public string Domain { get; set; } = DrawingDomain.MechanicalPlate;

    public IReadOnlyList<string> AllowedEntityTypes { get; set; } = Array.Empty<string>();

    public string LayerStandard { get; set; } = "enterprise-default-v1";

    public string DrawingSpecVersion { get; set; } = "1.0";
}

public sealed class AiDrawingSpecResponse
{
    public DrawingSpec? Spec { get; set; }

    public IReadOnlyList<string> Clarifications { get; set; } = Array.Empty<string>();

    public ValidationResult Validation { get; set; } = ValidationResult.Success();
}
