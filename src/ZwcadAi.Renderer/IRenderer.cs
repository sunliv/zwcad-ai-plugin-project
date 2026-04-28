using System;
using System.Collections.Generic;
using ZwcadAi.Core;

namespace ZwcadAi.Renderer;

public interface IRenderer
{
    RenderResult Render(DrawingSpec spec, RenderContext context);
}

public sealed class RenderContext
{
    public RenderContext(string requestId, string layerStandard)
    {
        RequestId = requestId;
        LayerStandard = layerStandard;
    }

    public string RequestId { get; }

    public string LayerStandard { get; }
}

public sealed class RenderResult
{
    public RenderResult(bool success, IReadOnlyList<RenderedEntity> entities, ValidationResult validation)
        : this(success ? RenderStatus.Success : RenderStatus.Failed, entities, validation, null)
    {
    }

    public RenderResult(RenderStatus status, IReadOnlyList<RenderedEntity> entities, ValidationResult validation)
        : this(status, entities, validation, null)
    {
    }

    public RenderResult(
        RenderStatus status,
        IReadOnlyList<RenderedEntity> entities,
        ValidationResult validation,
        GeometrySummary? summary)
    {
        Status = status;
        Entities = entities ?? Array.Empty<RenderedEntity>();
        Validation = validation ?? throw new ArgumentNullException(nameof(validation));
        Summary = summary ?? GeometrySummary.FromRenderedEntities(Status, Entities, Validation);
    }

    public RenderStatus Status { get; }

    public bool Success => Status == RenderStatus.Success;

    public bool Canceled => Status == RenderStatus.Canceled;

    public IReadOnlyList<RenderedEntity> Entities { get; }

    public ValidationResult Validation { get; }

    public GeometrySummary Summary { get; }
}

public enum RenderStatus
{
    Success,
    Failed,
    Canceled
}

public sealed class RenderedEntity
{
    public RenderedEntity(string specEntityId, string cadObjectId)
    {
        SpecEntityId = specEntityId;
        CadObjectId = cadObjectId;
    }

    public string SpecEntityId { get; }

    public string CadObjectId { get; }
}
