using System;
using System.Collections.Generic;
using System.Linq;
using ZwcadAi.Core;

namespace ZwcadAi.Renderer;

public sealed class GeometrySummary
{
    public GeometrySummary(
        RenderStatus status,
        int entityCount,
        int dimensionCount,
        int cadObjectCount,
        IReadOnlyDictionary<string, int> typeCounts,
        IReadOnlyDictionary<string, int> layerCounts,
        GeometryBounds? bounds,
        IReadOnlyDictionary<string, string> objectIdBySpecId,
        IReadOnlyList<ValidationIssue> issues,
        string outputPath,
        string exportStatus)
    {
        Status = status;
        EntityCount = entityCount;
        DimensionCount = dimensionCount;
        CadObjectCount = cadObjectCount;
        TypeCounts = typeCounts ?? throw new ArgumentNullException(nameof(typeCounts));
        LayerCounts = layerCounts ?? throw new ArgumentNullException(nameof(layerCounts));
        Bounds = bounds;
        ObjectIdBySpecId = objectIdBySpecId ?? throw new ArgumentNullException(nameof(objectIdBySpecId));
        Issues = issues ?? throw new ArgumentNullException(nameof(issues));
        OutputPath = outputPath ?? string.Empty;
        ExportStatus = string.IsNullOrWhiteSpace(exportStatus) ? "not_requested" : exportStatus;
    }

    public RenderStatus Status { get; }

    public bool Success => Status == RenderStatus.Success;

    public bool Canceled => Status == RenderStatus.Canceled;

    public int EntityCount { get; }

    public int DimensionCount { get; }

    public int CadObjectCount { get; }

    public IReadOnlyDictionary<string, int> TypeCounts { get; }

    public IReadOnlyDictionary<string, int> LayerCounts { get; }

    public GeometryBounds? Bounds { get; }

    public IReadOnlyDictionary<string, string> ObjectIdBySpecId { get; }

    public IReadOnlyList<ValidationIssue> Issues { get; }

    public string OutputPath { get; }

    public string ExportStatus { get; }

    public static GeometrySummary FromRenderedEntities(
        RenderStatus status,
        IReadOnlyList<RenderedEntity> renderedEntities,
        ValidationResult validation)
    {
        return new GeometrySummary(
            status,
            0,
            0,
            renderedEntities?.Count ?? 0,
            new Dictionary<string, int>(StringComparer.Ordinal),
            new Dictionary<string, int>(StringComparer.Ordinal),
            null,
            BuildObjectIdMap(renderedEntities ?? Array.Empty<RenderedEntity>()),
            validation?.Issues.ToArray() ?? Array.Empty<ValidationIssue>(),
            string.Empty,
            "not_requested");
    }

    public static GeometrySummary FromPlan(
        DrawingRenderPlan plan,
        RenderStatus status,
        IReadOnlyList<RenderedEntity> renderedEntities,
        ValidationResult validation)
    {
        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        var committed = status == RenderStatus.Success;
        var entities = committed ? plan.Entities : Array.Empty<PlannedEntity>();
        var dimensions = committed ? plan.Dimensions : Array.Empty<PlannedDimension>();

        return new GeometrySummary(
            status,
            entities.Count,
            dimensions.Count,
            renderedEntities?.Count ?? 0,
            BuildTypeCounts(entities, dimensions),
            BuildLayerCounts(entities, dimensions),
            committed ? BuildBounds(entities, dimensions) : null,
            committed ? BuildObjectIdMap(renderedEntities ?? Array.Empty<RenderedEntity>()) : new Dictionary<string, string>(StringComparer.Ordinal),
            validation?.Issues.ToArray() ?? Array.Empty<ValidationIssue>(),
            string.Empty,
            "not_requested");
    }

    private static IReadOnlyDictionary<string, int> BuildTypeCounts(
        IReadOnlyList<PlannedEntity> entities,
        IReadOnlyList<PlannedDimension> dimensions)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var entity in entities)
        {
            Increment(counts, entity.Kind.ToString());
        }

        foreach (var dimension in dimensions)
        {
            Increment(counts, dimension.Type);
        }

        return counts;
    }

    private static IReadOnlyDictionary<string, int> BuildLayerCounts(
        IReadOnlyList<PlannedEntity> entities,
        IReadOnlyList<PlannedDimension> dimensions)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var entity in entities)
        {
            Increment(counts, entity.Layer);
        }

        foreach (var dimension in dimensions)
        {
            Increment(counts, dimension.Layer);
        }

        return counts;
    }

    private static IReadOnlyDictionary<string, string> BuildObjectIdMap(IReadOnlyList<RenderedEntity> renderedEntities)
    {
        return renderedEntities
            .GroupBy(entity => entity.SpecEntityId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().CadObjectId, StringComparer.Ordinal);
    }

    private static void Increment(IDictionary<string, int> counts, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            key = "?";
        }

        counts[key] = counts.TryGetValue(key, out var current) ? current + 1 : 1;
    }

    private static GeometryBounds? BuildBounds(
        IReadOnlyList<PlannedEntity> entities,
        IReadOnlyList<PlannedDimension> dimensions)
    {
        var bounds = new GeometryBoundsBuilder();

        foreach (var entity in entities)
        {
            IncludeEntity(bounds, entity);
        }

        foreach (var dimension in dimensions)
        {
            IncludeDimension(bounds, dimension);
        }

        return bounds.TryBuild();
    }

    private static void IncludeEntity(GeometryBoundsBuilder bounds, PlannedEntity entity)
    {
        switch (entity.Kind)
        {
            case PlannedEntityKind.Polyline:
                foreach (var point in entity.Points)
                {
                    bounds.Include(point);
                }

                break;
            case PlannedEntityKind.Circle:
            case PlannedEntityKind.Arc:
                bounds.IncludeCircle(entity.Center, entity.Radius);
                break;
            case PlannedEntityKind.Line:
            case PlannedEntityKind.CenterLine:
                bounds.Include(entity.Start);
                bounds.Include(entity.End);
                break;
            case PlannedEntityKind.Text:
            case PlannedEntityKind.MText:
                bounds.Include(entity.Position);
                break;
        }
    }

    private static void IncludeDimension(GeometryBoundsBuilder bounds, PlannedDimension dimension)
    {
        bounds.Include(dimension.From);
        bounds.Include(dimension.To);
        bounds.Include(dimension.Center);

        if (dimension.From != null && dimension.Offset != null)
        {
            bounds.Include(new DrawingPoint(
                dimension.From.X + dimension.Offset.X,
                dimension.From.Y + dimension.Offset.Y));
        }

        if (dimension.Center != null && dimension.Offset != null)
        {
            bounds.Include(new DrawingPoint(
                dimension.Center.X + dimension.Offset.X,
                dimension.Center.Y + dimension.Offset.Y));
        }
    }

    private sealed class GeometryBoundsBuilder
    {
        private bool _hasPoint;
        private double _minX;
        private double _minY;
        private double _maxX;
        private double _maxY;

        public void Include(DrawingPoint? point)
        {
            if (point == null)
            {
                return;
            }

            Include(point.X, point.Y);
        }

        public void IncludeCircle(DrawingPoint? center, double radius)
        {
            if (center == null || radius <= 0)
            {
                return;
            }

            Include(center.X - radius, center.Y - radius);
            Include(center.X + radius, center.Y + radius);
        }

        public GeometryBounds? TryBuild()
        {
            return _hasPoint ? new GeometryBounds(_minX, _minY, _maxX, _maxY) : null;
        }

        private void Include(double x, double y)
        {
            if (!_hasPoint)
            {
                _minX = x;
                _minY = y;
                _maxX = x;
                _maxY = y;
                _hasPoint = true;
                return;
            }

            _minX = Math.Min(_minX, x);
            _minY = Math.Min(_minY, y);
            _maxX = Math.Max(_maxX, x);
            _maxY = Math.Max(_maxY, y);
        }
    }
}

public sealed class GeometryBounds
{
    public GeometryBounds(double minX, double minY, double maxX, double maxY)
    {
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    public double MinX { get; }

    public double MinY { get; }

    public double MaxX { get; }

    public double MaxY { get; }

    public double Width => MaxX - MinX;

    public double Height => MaxY - MinY;
}
