using System;
using System.Collections.Generic;
using System.Linq;
using ZwcadAi.Core;
using ZwcadAi.Renderer;
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.Colors;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.Geometry;

namespace ZwcadAi.Plugin;

public sealed class ZwcadDrawingWriter
{
    private static readonly IReadOnlyDictionary<PlannedEntityKind, Func<PlannedEntity, Entity>> EntityFactories =
        new Dictionary<PlannedEntityKind, Func<PlannedEntity, Entity>>
        {
            [PlannedEntityKind.Polyline] = CreatePolyline,
            [PlannedEntityKind.Circle] = CreateCircle,
            [PlannedEntityKind.Line] = CreateLine,
            [PlannedEntityKind.CenterLine] = CreateLine,
            [PlannedEntityKind.Arc] = CreateArc,
            [PlannedEntityKind.Text] = CreateText,
            [PlannedEntityKind.MText] = CreateMText
        };

    public RenderResult Render(DrawingRenderPlan plan)
    {
        return Render(plan, WriterRenderOptions.Default);
    }

    public RenderResult Render(DrawingRenderPlan plan, WriterRenderOptions? options)
    {
        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        var transactionBoundary = new WriterTransactionBoundary(CreateTransactionScope);
        return transactionBoundary.Execute(plan, options, AppendPlanToModelSpace);
    }

    private static IWriterTransactionScope CreateTransactionScope()
    {
        var document = Application.DocumentManager?.MdiActiveDocument
            ?? throw new InvalidOperationException("No active ZWCAD document is available.");
        var database = document.Database
            ?? throw new InvalidOperationException("No active ZWCAD database is available.");

        return new ZwcadWriterTransactionScope(document, database);
    }

    private static IReadOnlyList<RenderedEntity> AppendPlanToModelSpace(
        IWriterTransactionScope transactionScope,
        WriterTransactionContext context)
    {
        var zwcadTransaction = transactionScope as ZwcadWriterTransactionScope
            ?? throw new InvalidOperationException("ZWCAD writer received an unexpected transaction scope.");
        var database = zwcadTransaction.Database;
        var transaction = zwcadTransaction.Transaction;
        var plan = context.Plan;
        var renderedEntities = new List<RenderedEntity>();

        EnsureLayers(database, transaction, plan.Layers);

        var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        var modelSpace = (BlockTableRecord)transaction.GetObject(
            blockTable[BlockTableRecord.ModelSpace],
            OpenMode.ForWrite);

        foreach (var plannedEntity in plan.Entities)
        {
            context.ThrowIfCancellationRequested();

            try
            {
                var cadEntity = CreateCadEntity(plannedEntity);
                var objectId = modelSpace.AppendEntity(cadEntity);
                transaction.AddNewlyCreatedDBObject(cadEntity, true);
                var renderedEntity = new RenderedEntity(plannedEntity.SpecEntityId, objectId.ToString());
                renderedEntities.Add(renderedEntity);
                context.AfterEntityAppended(plannedEntity, renderedEntity);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (WriterRenderException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw WriterRenderException.ForEntity(plannedEntity, exception.Message);
            }
        }

        foreach (var plannedDimension in plan.Dimensions)
        {
            context.ThrowIfCancellationRequested();

            try
            {
                var cadDimension = CreateCadDimension(database, plan, plannedDimension);
                var objectId = modelSpace.AppendEntity(cadDimension);
                transaction.AddNewlyCreatedDBObject(cadDimension, true);
                var renderedEntity = new RenderedEntity(plannedDimension.SpecDimensionId, objectId.ToString());
                renderedEntities.Add(renderedEntity);
                context.AfterDimensionAppended(plannedDimension, renderedEntity);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (WriterRenderException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw WriterRenderException.ForDimension(plannedDimension, exception.Message);
            }
        }

        return renderedEntities;
    }

    private static void EnsureLayers(Database database, Transaction transaction, IEnumerable<LayerSpec> layers)
    {
        var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        var linetypeTable = (LinetypeTable)transaction.GetObject(database.LinetypeTableId, OpenMode.ForRead);

        foreach (var layer in layers)
        {
            if (layerTable.Has(layer.Name))
            {
                continue;
            }

            layerTable.UpgradeOpen();

            var layerRecord = new LayerTableRecord
            {
                Name = layer.Name,
                Color = Color.FromColorIndex(ColorMethod.ByAci, (short)layer.Color),
                LineWeight = ToLineWeight(layer.LineWeight)
            };

            if (!string.IsNullOrWhiteSpace(layer.LineType) && linetypeTable.Has(layer.LineType))
            {
                layerRecord.LinetypeObjectId = linetypeTable[layer.LineType];
            }

            layerTable.Add(layerRecord);
            transaction.AddNewlyCreatedDBObject(layerRecord, true);
        }
    }

    private static Entity CreateCadEntity(PlannedEntity plannedEntity)
    {
        if (!EntityFactories.TryGetValue(plannedEntity.Kind, out var factory))
        {
            throw CreateEntityFailure(
                plannedEntity,
                $"Unsupported entity kind '{plannedEntity.Kind}'.");
        }

        var cadEntity = factory(plannedEntity);
        cadEntity.Layer = plannedEntity.Layer;
        return cadEntity;
    }

    private static Polyline CreatePolyline(PlannedEntity plannedEntity)
    {
        if (plannedEntity.Points.Count < 2)
        {
            throw CreateEntityFailure(plannedEntity, "Polyline has fewer than two points.");
        }

        var polyline = new Polyline(plannedEntity.Points.Count)
        {
            Closed = plannedEntity.Closed
        };

        for (var index = 0; index < plannedEntity.Points.Count; index++)
        {
            polyline.AddVertexAt(index, ToPoint2d(plannedEntity.Points[index]), 0, 0, 0);
        }

        return polyline;
    }

    private static Circle CreateCircle(PlannedEntity plannedEntity)
    {
        if (plannedEntity.Center == null || plannedEntity.Radius <= 0)
        {
            throw CreateEntityFailure(plannedEntity, "Circle has invalid geometry.");
        }

        return new Circle(ToPoint3d(plannedEntity.Center), Vector3d.ZAxis, plannedEntity.Radius);
    }

    private static Line CreateLine(PlannedEntity plannedEntity)
    {
        if (plannedEntity.Start == null || plannedEntity.End == null)
        {
            throw CreateEntityFailure(plannedEntity, "Line has invalid geometry.");
        }

        return new Line(ToPoint3d(plannedEntity.Start), ToPoint3d(plannedEntity.End));
    }

    private static Arc CreateArc(PlannedEntity plannedEntity)
    {
        if (plannedEntity.Center == null || plannedEntity.Radius <= 0)
        {
            throw CreateEntityFailure(plannedEntity, "Arc has invalid geometry.");
        }

        return new Arc(
            ToPoint3d(plannedEntity.Center),
            Vector3d.ZAxis,
            plannedEntity.Radius,
            ToRadians(plannedEntity.StartAngle),
            ToRadians(plannedEntity.EndAngle));
    }

    private static DBText CreateText(PlannedEntity plannedEntity)
    {
        if (plannedEntity.Position == null || plannedEntity.Height <= 0)
        {
            throw CreateEntityFailure(plannedEntity, "Text entity has invalid geometry.");
        }

        return new DBText
        {
            Position = ToPoint3d(plannedEntity.Position),
            TextString = plannedEntity.Value,
            Height = plannedEntity.Height,
            Rotation = ToRadians(plannedEntity.Rotation)
        };
    }

    private static MText CreateMText(PlannedEntity plannedEntity)
    {
        if (plannedEntity.Position == null || plannedEntity.Height <= 0)
        {
            throw CreateEntityFailure(plannedEntity, "MText entity has invalid geometry.");
        }

        return new MText
        {
            Location = ToPoint3d(plannedEntity.Position),
            Contents = plannedEntity.Value,
            TextHeight = plannedEntity.Height,
            Rotation = ToRadians(plannedEntity.Rotation)
        };
    }

    private static Entity CreateCadDimension(Database database, DrawingRenderPlan plan, PlannedDimension plannedDimension)
    {
        Entity dimension;
        switch (plannedDimension.Type)
        {
            case DimensionTypes.Linear:
                dimension = CreateLinearDimension(database, plannedDimension);
                break;
            case DimensionTypes.Aligned:
                dimension = CreateAlignedDimension(database, plannedDimension);
                break;
            case DimensionTypes.Radius:
                dimension = CreateRadiusDimension(database, plan, plannedDimension);
                break;
            case DimensionTypes.Diameter:
                dimension = CreateDiameterDimension(database, plan, plannedDimension);
                break;
            case DimensionTypes.Angular:
                dimension = CreateAngularDimension(database, plannedDimension);
                break;
            default:
                throw CreateDimensionFailure(
                    plannedDimension,
                    $"Unsupported dimension type '{plannedDimension.Type}'.");
        }

        dimension.Layer = plannedDimension.Layer;
        return dimension;
    }

    private static RotatedDimension CreateLinearDimension(Database database, PlannedDimension plannedDimension)
    {
        if (plannedDimension.From == null || plannedDimension.To == null || plannedDimension.Offset == null)
        {
            throw CreateDimensionFailure(plannedDimension, "Linear dimension has invalid geometry.");
        }

        var rotation = Math.Atan2(
            plannedDimension.To.Y - plannedDimension.From.Y,
            plannedDimension.To.X - plannedDimension.From.X);
        var dimensionLinePoint = ToOffsetPoint3d(plannedDimension.From, plannedDimension.Offset);

        return new RotatedDimension(
            rotation,
            ToPoint3d(plannedDimension.From),
            ToPoint3d(plannedDimension.To),
            dimensionLinePoint,
            plannedDimension.Text,
            database.Dimstyle);
    }

    private static AlignedDimension CreateAlignedDimension(Database database, PlannedDimension plannedDimension)
    {
        if (plannedDimension.From == null || plannedDimension.To == null || plannedDimension.Offset == null)
        {
            throw CreateDimensionFailure(plannedDimension, "Aligned dimension has invalid geometry.");
        }

        return new AlignedDimension(
            ToPoint3d(plannedDimension.From),
            ToPoint3d(plannedDimension.To),
            ToOffsetPoint3d(plannedDimension.From, plannedDimension.Offset),
            plannedDimension.Text,
            database.Dimstyle);
    }

    private static RadialDimension CreateRadiusDimension(
        Database database,
        DrawingRenderPlan plan,
        PlannedDimension plannedDimension)
    {
        var targetEntity = plan.Entities.SingleOrDefault(entity =>
            string.Equals(entity.SpecEntityId, plannedDimension.TargetEntityId, StringComparison.Ordinal)
            && (entity.Kind == PlannedEntityKind.Circle || entity.Kind == PlannedEntityKind.Arc));

        if (targetEntity?.Center == null || targetEntity.Radius <= 0)
        {
            throw CreateDimensionFailure(
                plannedDimension,
                $"Radius dimension targets missing or invalid entity '{plannedDimension.TargetEntityId}'.");
        }

        var angle = targetEntity.Kind == PlannedEntityKind.Arc ? ToRadians(targetEntity.StartAngle) : 0d;
        var chordPoint = new Point3d(
            targetEntity.Center.X + targetEntity.Radius * Math.Cos(angle),
            targetEntity.Center.Y + targetEntity.Radius * Math.Sin(angle),
            0);

        return new RadialDimension(
            ToPoint3d(targetEntity.Center),
            chordPoint,
            0,
            plannedDimension.Text,
            database.Dimstyle);
    }

    private static DiametricDimension CreateDiameterDimension(
        Database database,
        DrawingRenderPlan plan,
        PlannedDimension plannedDimension)
    {
        var targetCircle = plan.Entities.SingleOrDefault(entity =>
            string.Equals(entity.SpecEntityId, plannedDimension.TargetEntityId, StringComparison.Ordinal)
            && entity.Kind == PlannedEntityKind.Circle);

        if (targetCircle?.Center == null || targetCircle.Radius <= 0)
        {
            throw CreateDimensionFailure(
                plannedDimension,
                $"Diameter dimension targets missing or invalid circle '{plannedDimension.TargetEntityId}'.");
        }

        var left = new Point3d(targetCircle.Center.X - targetCircle.Radius, targetCircle.Center.Y, 0);
        var right = new Point3d(targetCircle.Center.X + targetCircle.Radius, targetCircle.Center.Y, 0);

        return new DiametricDimension(left, right, 0, plannedDimension.Text, database.Dimstyle);
    }

    private static Point3AngularDimension CreateAngularDimension(Database database, PlannedDimension plannedDimension)
    {
        if (plannedDimension.Center == null
            || plannedDimension.From == null
            || plannedDimension.To == null
            || plannedDimension.Offset == null)
        {
            throw CreateDimensionFailure(plannedDimension, "Angular dimension has invalid geometry.");
        }

        return new Point3AngularDimension(
            ToPoint3d(plannedDimension.Center),
            ToPoint3d(plannedDimension.From),
            ToPoint3d(plannedDimension.To),
            ToOffsetPoint3d(plannedDimension.Center, plannedDimension.Offset),
            plannedDimension.Text,
            database.Dimstyle);
    }

    private static Point2d ToPoint2d(DrawingPoint point)
    {
        return new Point2d(point.X, point.Y);
    }

    private static Point3d ToPoint3d(DrawingPoint point)
    {
        return new Point3d(point.X, point.Y, 0);
    }

    private static Point3d ToOffsetPoint3d(DrawingPoint anchor, DrawingPoint offset)
    {
        return new Point3d(anchor.X + offset.X, anchor.Y + offset.Y, 0);
    }

    private static WriterRenderException CreateEntityFailure(PlannedEntity plannedEntity, string message)
    {
        var path = $"$.entities[{plannedEntity.SpecEntityId}]";
        return new WriterRenderException("entity_render_failed", path, $"Entity '{path}' failed to render: {message}");
    }

    private static WriterRenderException CreateDimensionFailure(PlannedDimension plannedDimension, string message)
    {
        var path = $"$.dimensions[{plannedDimension.SpecDimensionId}]";
        return new WriterRenderException("dimension_render_failed", path, $"Dimension '{path}' failed to render: {message}");
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180d;
    }

    private static LineWeight ToLineWeight(double lineWeightMm)
    {
        if (lineWeightMm >= 0.35)
        {
            return LineWeight.LineWeight035;
        }

        if (lineWeightMm >= 0.25)
        {
            return LineWeight.LineWeight025;
        }

        if (lineWeightMm >= 0.18)
        {
            return LineWeight.LineWeight018;
        }

        if (lineWeightMm >= 0.09)
        {
            return LineWeight.LineWeight009;
        }

        return LineWeight.ByLayer;
    }

    private sealed class ZwcadWriterTransactionScope : IWriterTransactionScope
    {
        private readonly IDisposable _documentLock;

        public ZwcadWriterTransactionScope(Document document, Database database)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            Database = database ?? throw new ArgumentNullException(nameof(database));
            _documentLock = document.LockDocument();
            Transaction = Database.TransactionManager.StartTransaction();
        }

        public Database Database { get; }

        public Transaction Transaction { get; }

        public void Commit()
        {
            Transaction.Commit();
        }

        public void Dispose()
        {
            Transaction.Dispose();
            _documentLock.Dispose();
        }
    }
}
