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

    public DrawingPoint? Position { get; set; }

    public double Radius { get; set; }

    public double StartAngle { get; set; }

    public double EndAngle { get; set; }

    public string Value { get; set; } = string.Empty;

    public double Height { get; set; }

    public double Rotation { get; set; }
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
    Text,
    MText
}

public sealed class DrawingSpecPlanRenderer : IRenderer
{
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
                case EntityTypes.Arc:
                    plannedEntities.Add(new PlannedEntity(entity.Id, entity.Id, PlannedEntityKind.Arc, entity.Layer)
                    {
                        Center = entity.Center,
                        Radius = entity.Radius,
                        StartAngle = entity.StartAngle,
                        EndAngle = entity.EndAngle
                    });
                    break;
                case EntityTypes.Text:
                    plannedEntities.Add(CreatePlannedTextEntity(entity, PlannedEntityKind.Text));
                    break;
                case EntityTypes.MText:
                    plannedEntities.Add(CreatePlannedTextEntity(entity, PlannedEntityKind.MText));
                    break;
                default:
                    issues.Add(new ValidationIssue(
                        "unsupported_entity_type",
                        $"$.entities[{entity.Id}].type",
                        $"Entity type '{entity.Type}' is not supported by the P3 renderer.",
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

    private static PlannedEntity CreatePlannedTextEntity(EntitySpec entity, PlannedEntityKind kind)
    {
        return new PlannedEntity(entity.Id, entity.Id, kind, entity.Layer)
        {
            Position = entity.Position,
            Value = entity.Value,
            Height = entity.Height,
            Rotation = entity.Rotation
        };
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
        return DrawingSpecValidator.ValidateBusinessRules(spec).Issues.ToList();
    }
}
