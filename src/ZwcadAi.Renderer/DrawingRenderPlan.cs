using System;
using System.Collections.Generic;
using System.Linq;
using ZwcadAi.Core;

namespace ZwcadAi.Renderer;

public sealed class DrawingRenderPlan
{
    public DrawingRenderPlan(
        IReadOnlyList<LayerSpec> layers,
        IReadOnlyList<PlannedEntity> entities,
        IReadOnlyList<PlannedDimension> dimensions,
        ValidationResult validation)
    {
        Layers = layers ?? throw new ArgumentNullException(nameof(layers));
        Entities = entities ?? throw new ArgumentNullException(nameof(entities));
        Dimensions = dimensions ?? throw new ArgumentNullException(nameof(dimensions));
        Validation = validation ?? throw new ArgumentNullException(nameof(validation));
    }

    public IReadOnlyList<LayerSpec> Layers { get; }

    public IReadOnlyList<PlannedEntity> Entities { get; }

    public IReadOnlyList<PlannedDimension> Dimensions { get; }

    public ValidationResult Validation { get; }
}

public sealed class PlannedEntity
{
    public PlannedEntity(string specEntityId, string sourceEntityId, PlannedEntityKind kind, string layer)
    {
        if (string.IsNullOrWhiteSpace(specEntityId))
        {
            throw new ArgumentException("Planned entity id is required.", nameof(specEntityId));
        }

        SpecEntityId = specEntityId;
        SourceEntityId = string.IsNullOrWhiteSpace(sourceEntityId) ? specEntityId : sourceEntityId;
        Kind = kind;
        Layer = layer ?? string.Empty;
    }

    public string SpecEntityId { get; }

    public string SourceEntityId { get; }

    public PlannedEntityKind Kind { get; }

    public string Layer { get; }

    public bool Closed { get; set; }

    public IReadOnlyList<DrawingPoint> Points { get; set; } = Array.Empty<DrawingPoint>();

    public DrawingPoint? Start { get; set; }

    public DrawingPoint? End { get; set; }

    public DrawingPoint? Center { get; set; }

    public double Radius { get; set; }
}

public sealed class PlannedDimension
{
    public PlannedDimension(string specDimensionId, string type, string layer)
    {
        if (string.IsNullOrWhiteSpace(specDimensionId))
        {
            throw new ArgumentException("Planned dimension id is required.", nameof(specDimensionId));
        }

        SpecDimensionId = specDimensionId;
        Type = type ?? string.Empty;
        Layer = layer ?? string.Empty;
    }

    public string SpecDimensionId { get; }

    public string Type { get; }

    public string Layer { get; }

    public DrawingPoint? From { get; set; }

    public DrawingPoint? To { get; set; }

    public DrawingPoint? Center { get; set; }

    public string TargetEntityId { get; set; } = string.Empty;

    public DrawingPoint? Offset { get; set; }

    public string Text { get; set; } = string.Empty;
}

public enum PlannedEntityKind
{
    Line,
    Polyline,
    Circle,
    CenterLine,
    Arc,
    Text
}

public sealed class DrawingSpecPlanRenderer : IRenderer
{
    private static readonly HashSet<string> AllowedLayers = new HashSet<string>(
        new[] { CadLayerNames.Outline, CadLayerNames.Center, CadLayerNames.Dimension },
        StringComparer.Ordinal);

    public DrawingRenderPlan CreatePlan(DrawingSpec spec)
    {
        if (spec == null)
        {
            throw new ArgumentNullException(nameof(spec));
        }

        var issues = Validate(spec);
        if (issues.Count > 0)
        {
            return new DrawingRenderPlan(
                spec.Layers.ToArray(),
                Array.Empty<PlannedEntity>(),
                Array.Empty<PlannedDimension>(),
                ValidationResult.Failure(issues));
        }

        var plannedEntities = new List<PlannedEntity>();
        foreach (var entity in spec.Entities)
        {
            switch (entity.Type)
            {
                case EntityTypes.Polyline:
                    plannedEntities.Add(new PlannedEntity(entity.Id, entity.Id, PlannedEntityKind.Polyline, entity.Layer)
                    {
                        Closed = entity.Closed,
                        Points = entity.Points.ToArray()
                    });
                    break;
                case EntityTypes.Circle:
                    plannedEntities.Add(new PlannedEntity(entity.Id, entity.Id, PlannedEntityKind.Circle, entity.Layer)
                    {
                        Center = entity.Center,
                        Radius = entity.Radius
                    });
                    break;
                case EntityTypes.CenterMark:
                    AddCenterMarkLines(plannedEntities, entity);
                    break;
                case EntityTypes.Line:
                    plannedEntities.Add(new PlannedEntity(entity.Id, entity.Id, PlannedEntityKind.Line, entity.Layer)
                    {
                        Start = entity.Start,
                        End = entity.End
                    });
                    break;
                default:
                    issues.Add(new ValidationIssue(
                        "unsupported_entity_type",
                        $"entities[{entity.Id}].type",
                        $"Entity type '{entity.Type}' is not supported by the P1-03 renderer.",
                        ValidationSeverity.Error));
                    break;
            }
        }

        if (issues.Count > 0)
        {
            return new DrawingRenderPlan(
                spec.Layers.ToArray(),
                Array.Empty<PlannedEntity>(),
                Array.Empty<PlannedDimension>(),
                ValidationResult.Failure(issues));
        }

        var plannedDimensions = spec.Dimensions
            .Select(dimension => new PlannedDimension(dimension.Id, dimension.Type, dimension.Layer)
            {
                From = dimension.From,
                To = dimension.To,
                Center = dimension.Center,
                TargetEntityId = dimension.TargetEntityId,
                Offset = dimension.Offset,
                Text = dimension.Text
            })
            .ToArray();

        return new DrawingRenderPlan(
            spec.Layers.ToArray(),
            plannedEntities,
            plannedDimensions,
            ValidationResult.Success());
    }

    public RenderResult Render(DrawingSpec spec, RenderContext context)
    {
        var plan = CreatePlan(spec);
        if (!plan.Validation.IsValid)
        {
            return new RenderResult(false, Array.Empty<RenderedEntity>(), plan.Validation);
        }

        var renderedEntities = plan.Entities
            .Select(entity => new RenderedEntity(entity.SpecEntityId, $"planned:{entity.SpecEntityId}"))
            .Concat(plan.Dimensions.Select(dimension => new RenderedEntity(dimension.SpecDimensionId, $"planned:{dimension.SpecDimensionId}")))
            .ToArray();

        return new RenderResult(true, renderedEntities, plan.Validation);
    }

    private static void AddCenterMarkLines(ICollection<PlannedEntity> plannedEntities, EntitySpec entity)
    {
        var center = entity.Center!;
        var size = entity.Size;

        plannedEntities.Add(new PlannedEntity($"{entity.Id}-horizontal", entity.Id, PlannedEntityKind.CenterLine, entity.Layer)
        {
            Start = new DrawingPoint(center.X - size, center.Y),
            End = new DrawingPoint(center.X + size, center.Y)
        });
        plannedEntities.Add(new PlannedEntity($"{entity.Id}-vertical", entity.Id, PlannedEntityKind.CenterLine, entity.Layer)
        {
            Start = new DrawingPoint(center.X, center.Y - size),
            End = new DrawingPoint(center.X, center.Y + size)
        });
    }

    private static List<ValidationIssue> Validate(DrawingSpec spec)
    {
        var issues = new List<ValidationIssue>();
        var layerNames = new HashSet<string>(spec.Layers.Select(layer => layer.Name), StringComparer.Ordinal);

        foreach (var layer in spec.Layers)
        {
            if (!AllowedLayers.Contains(layer.Name))
            {
                issues.Add(new ValidationIssue(
                    "unsupported_layer",
                    $"layers[{layer.Name}]",
                    $"Layer '{layer.Name}' is outside the P1-03 standard whitelist.",
                    ValidationSeverity.Error));
            }
        }

        foreach (var entity in spec.Entities)
        {
            if (!layerNames.Contains(entity.Layer))
            {
                issues.Add(new ValidationIssue(
                    "missing_entity_layer",
                    $"entities[{entity.Id}].layer",
                    $"Entity '{entity.Id}' references missing layer '{entity.Layer}'.",
                    ValidationSeverity.Error));
            }

            ValidateEntityGeometry(entity, issues);
        }

        var entityIds = new HashSet<string>(spec.Entities.Select(entity => entity.Id), StringComparer.Ordinal);
        foreach (var dimension in spec.Dimensions)
        {
            if (!layerNames.Contains(dimension.Layer))
            {
                issues.Add(new ValidationIssue(
                    "missing_dimension_layer",
                    $"dimensions[{dimension.Id}].layer",
                    $"Dimension '{dimension.Id}' references missing layer '{dimension.Layer}'.",
                    ValidationSeverity.Error));
            }

            if (!string.Equals(dimension.Layer, CadLayerNames.Dimension, StringComparison.Ordinal))
            {
                issues.Add(new ValidationIssue(
                    "invalid_dimension_layer",
                    $"dimensions[{dimension.Id}].layer",
                    $"Dimension '{dimension.Id}' must be on layer '{CadLayerNames.Dimension}'.",
                    ValidationSeverity.Error));
            }

            if (!string.IsNullOrWhiteSpace(dimension.TargetEntityId) && !entityIds.Contains(dimension.TargetEntityId))
            {
                issues.Add(new ValidationIssue(
                    "missing_dimension_target",
                    $"dimensions[{dimension.Id}].targetEntityId",
                    $"Dimension '{dimension.Id}' targets missing entity '{dimension.TargetEntityId}'.",
                    ValidationSeverity.Error));
            }
        }

        return issues;
    }

    private static void ValidateEntityGeometry(EntitySpec entity, ICollection<ValidationIssue> issues)
    {
        switch (entity.Type)
        {
            case EntityTypes.Polyline:
                if (entity.Points.Count < 2)
                {
                    issues.Add(new ValidationIssue(
                        "invalid_polyline_points",
                        $"entities[{entity.Id}].points",
                        $"Polyline '{entity.Id}' must contain at least two points.",
                        ValidationSeverity.Error));
                }

                break;
            case EntityTypes.Circle:
                if (entity.Center == null || entity.Radius <= 0)
                {
                    issues.Add(new ValidationIssue(
                        "invalid_circle_geometry",
                        $"entities[{entity.Id}]",
                        $"Circle '{entity.Id}' must have center and positive radius.",
                        ValidationSeverity.Error));
                }

                break;
            case EntityTypes.CenterMark:
                if (entity.Center == null || entity.Size <= 0)
                {
                    issues.Add(new ValidationIssue(
                        "invalid_center_mark_geometry",
                        $"entities[{entity.Id}]",
                        $"Center mark '{entity.Id}' must have center and positive size.",
                        ValidationSeverity.Error));
                }

                if (!string.Equals(entity.Layer, CadLayerNames.Center, StringComparison.Ordinal))
                {
                    issues.Add(new ValidationIssue(
                        "invalid_center_mark_layer",
                        $"entities[{entity.Id}].layer",
                        $"Center mark '{entity.Id}' must be on layer '{CadLayerNames.Center}'.",
                        ValidationSeverity.Error));
                }

                break;
            case EntityTypes.Line:
                if (entity.Start == null || entity.End == null)
                {
                    issues.Add(new ValidationIssue(
                        "invalid_line_geometry",
                        $"entities[{entity.Id}]",
                        $"Line '{entity.Id}' must have start and end points.",
                        ValidationSeverity.Error));
                }

                break;
        }
    }
}
