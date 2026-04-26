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
    {
        Success = success;
        Entities = entities;
        Validation = validation;
    }

    public bool Success { get; }

    public IReadOnlyList<RenderedEntity> Entities { get; }

    public ValidationResult Validation { get; }
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
