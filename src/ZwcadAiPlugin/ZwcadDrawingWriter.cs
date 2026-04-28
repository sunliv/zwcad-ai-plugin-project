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

    private static readonly IReadOnlyList<string> StandardLinetypeFiles =
        new[] { "zwcad.lin", "zwcadiso.lin", "acad.lin" };

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
        var textStyleIds = EnsureTextStyles(database, transaction);
        var dimensionStyleIds = EnsureDimensionStyles(database, transaction, textStyleIds);

        var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        var modelSpace = (BlockTableRecord)transaction.GetObject(
            blockTable[BlockTableRecord.ModelSpace],
            OpenMode.ForWrite);

        foreach (var plannedEntity in plan.Entities)
        {
            context.ThrowIfCancellationRequested();

            try
            {
                var cadEntity = CreateCadEntity(plannedEntity, textStyleIds);
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
                var cadDimension = CreateCadDimension(plan, plannedDimension, dimensionStyleIds);
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
            if (!CadLayerStandards.TryGet(layer.Name, out _))
            {
                throw new WriterRenderException(
                    "unsupported_layer",
                    $"$.layers[{layer.Name}].name",
                    $"Layer '{layer.Name}' is outside enterprise-default-v1.");
            }

            LayerTableRecord layerRecord;
            if (layerTable.Has(layer.Name))
            {
                layerRecord = (LayerTableRecord)transaction.GetObject(layerTable[layer.Name], OpenMode.ForWrite);
            }
            else
            {
                layerTable.UpgradeOpen();

                layerRecord = new LayerTableRecord
                {
                    Name = layer.Name
                };
                layerTable.Add(layerRecord);
                transaction.AddNewlyCreatedDBObject(layerRecord, true);
            }

            ApplyLayerStandard(database, layerRecord, layer, linetypeTable);
        }
    }

    private static void ApplyLayerStandard(
        Database database,
        LayerTableRecord layerRecord,
        LayerSpec layer,
        LinetypeTable linetypeTable)
    {
        layerRecord.Color = Color.FromColorIndex(ColorMethod.ByAci, (short)layer.Color);
        layerRecord.LineWeight = ToLineWeight(layer.LineWeight);
        layerRecord.IsPlottable = !string.Equals(layer.Name, CadLayerNames.Construction, StringComparison.Ordinal);

        var linetypeId = ResolveLinetypeId(database, linetypeTable, layer);
        if (!linetypeId.IsNull)
        {
            layerRecord.LinetypeObjectId = linetypeId;
        }
    }

    private static ObjectId ResolveLinetypeId(Database database, LinetypeTable linetypeTable, LayerSpec layer)
    {
        if (string.IsNullOrWhiteSpace(layer.LineType))
        {
            return ObjectId.Null;
        }

        if (linetypeTable.Has(layer.LineType))
        {
            return linetypeTable[layer.LineType];
        }

        if (string.Equals(layer.LineType, "Continuous", StringComparison.Ordinal))
        {
            return ObjectId.Null;
        }

        TryLoadStandardLinetype(database, linetypeTable, layer.LineType);
        if (linetypeTable.Has(layer.LineType))
        {
            return linetypeTable[layer.LineType];
        }

        throw new WriterRenderException(
            "missing_linetype",
            $"$.layers[{layer.Name}].lineType",
            $"Layer '{layer.Name}' requires missing linetype '{layer.LineType}'.");
    }

    private static void TryLoadStandardLinetype(Database database, LinetypeTable linetypeTable, string lineType)
    {
        foreach (var fileName in StandardLinetypeFiles)
        {
            try
            {
                database.LoadLineTypeFile(lineType, fileName);
            }
            catch (Exception)
            {
                // Fall through to the next standard file and keep the final error path deterministic.
            }

            if (linetypeTable.Has(lineType))
            {
                return;
            }
        }
    }

    private static IReadOnlyDictionary<string, ObjectId> EnsureTextStyles(Database database, Transaction transaction)
    {
        var textStyleTable = (TextStyleTable)transaction.GetObject(database.TextStyleTableId, OpenMode.ForRead);
        var styleIds = new Dictionary<string, ObjectId>(StringComparer.Ordinal);

        foreach (var standard in CadTextStyleStandards.Definitions.Values)
        {
            styleIds[standard.Name] = EnsureTextStyle(textStyleTable, transaction, standard);
        }

        return styleIds;
    }

    private static ObjectId EnsureTextStyle(
        TextStyleTable textStyleTable,
        Transaction transaction,
        CadTextStyleStandard standard)
    {
        TextStyleTableRecord textStyleRecord;
        if (textStyleTable.Has(standard.Name))
        {
            textStyleRecord = (TextStyleTableRecord)transaction.GetObject(textStyleTable[standard.Name], OpenMode.ForWrite);
        }
        else
        {
            textStyleTable.UpgradeOpen();
            textStyleRecord = new TextStyleTableRecord
            {
                Name = standard.Name
            };
            textStyleTable.Add(textStyleRecord);
            transaction.AddNewlyCreatedDBObject(textStyleRecord, true);
        }

        ApplyTextStyleStandard(textStyleRecord, standard);
        return textStyleRecord.ObjectId;
    }

    private static void ApplyTextStyleStandard(TextStyleTableRecord textStyleRecord, CadTextStyleStandard standard)
    {
        textStyleRecord.FileName = standard.FontFileName;
        textStyleRecord.TextSize = standard.Height;
        textStyleRecord.XScale = standard.WidthFactor;
        textStyleRecord.ObliquingAngle = standard.ObliqueAngle;
    }

    private static IReadOnlyDictionary<string, ObjectId> EnsureDimensionStyles(
        Database database,
        Transaction transaction,
        IReadOnlyDictionary<string, ObjectId> textStyleIds)
    {
        var dimStyleTable = (DimStyleTable)transaction.GetObject(database.DimStyleTableId, OpenMode.ForRead);
        var styleIds = new Dictionary<string, ObjectId>(StringComparer.Ordinal);

        foreach (var standard in CadDimensionStyleStandards.Definitions.Values)
        {
            styleIds[standard.Name] = EnsureDimensionStyle(dimStyleTable, transaction, textStyleIds, standard);
        }

        return styleIds;
    }

    private static ObjectId EnsureDimensionStyle(
        DimStyleTable dimStyleTable,
        Transaction transaction,
        IReadOnlyDictionary<string, ObjectId> textStyleIds,
        CadDimensionStyleStandard standard)
    {
        DimStyleTableRecord dimStyleRecord;
        if (dimStyleTable.Has(standard.Name))
        {
            dimStyleRecord = (DimStyleTableRecord)transaction.GetObject(dimStyleTable[standard.Name], OpenMode.ForWrite);
        }
        else
        {
            dimStyleTable.UpgradeOpen();
            dimStyleRecord = new DimStyleTableRecord
            {
                Name = standard.Name
            };
            dimStyleTable.Add(dimStyleRecord);
            transaction.AddNewlyCreatedDBObject(dimStyleRecord, true);
        }

        ApplyDimensionStyleStandard(dimStyleRecord, textStyleIds, standard);
        return dimStyleRecord.ObjectId;
    }

    private static void ApplyDimensionStyleStandard(
        DimStyleTableRecord dimStyleRecord,
        IReadOnlyDictionary<string, ObjectId> textStyleIds,
        CadDimensionStyleStandard standard)
    {
        dimStyleRecord.Dimtxsty = textStyleIds[standard.TextStyleName];
        dimStyleRecord.Dimtxt = standard.TextHeight;
        dimStyleRecord.Dimasz = standard.ArrowSize;
        dimStyleRecord.Dimexo = standard.ExtensionOffset;
        dimStyleRecord.Dimexe = standard.ExtensionBeyond;
        dimStyleRecord.Dimdec = standard.Precision;
    }

    private static Entity CreateCadEntity(
        PlannedEntity plannedEntity,
        IReadOnlyDictionary<string, ObjectId> textStyleIds)
    {
        if (!EntityFactories.TryGetValue(plannedEntity.Kind, out var factory))
        {
            throw CreateEntityFailure(
                plannedEntity,
                $"Unsupported entity kind '{plannedEntity.Kind}'.");
        }

        var cadEntity = factory(plannedEntity);
        cadEntity.Layer = plannedEntity.Layer;
        ApplyEntityStyle(cadEntity, plannedEntity, textStyleIds);
        return cadEntity;
    }

    private static void ApplyEntityStyle(
        Entity cadEntity,
        PlannedEntity plannedEntity,
        IReadOnlyDictionary<string, ObjectId> textStyleIds)
    {
        if (cadEntity is DBText text)
        {
            text.TextStyleId = ResolveTextStyleId(plannedEntity, textStyleIds);
            return;
        }

        if (cadEntity is MText mtext)
        {
            mtext.TextStyleId = ResolveTextStyleId(plannedEntity, textStyleIds);
        }
    }

    private static ObjectId ResolveTextStyleId(
        PlannedEntity plannedEntity,
        IReadOnlyDictionary<string, ObjectId> textStyleIds)
    {
        var textStyleName = string.IsNullOrWhiteSpace(plannedEntity.TextStyleName)
            ? CadTextStyleStandards.ResolveForLayer(plannedEntity.Layer, plannedEntity.Height).Name
            : plannedEntity.TextStyleName;

        return textStyleIds[textStyleName];
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

    private static Entity CreateCadDimension(
        DrawingRenderPlan plan,
        PlannedDimension plannedDimension,
        IReadOnlyDictionary<string, ObjectId> dimensionStyleIds)
    {
        var dimensionStyleId = ResolveDimensionStyleId(plannedDimension, dimensionStyleIds);
        Entity dimension;
        switch (plannedDimension.Type)
        {
            case DimensionTypes.Linear:
                dimension = CreateLinearDimension(dimensionStyleId, plannedDimension);
                break;
            case DimensionTypes.Aligned:
                dimension = CreateAlignedDimension(dimensionStyleId, plannedDimension);
                break;
            case DimensionTypes.Radius:
                dimension = CreateRadiusDimension(dimensionStyleId, plan, plannedDimension);
                break;
            case DimensionTypes.Diameter:
                dimension = CreateDiameterDimension(dimensionStyleId, plan, plannedDimension);
                break;
            case DimensionTypes.Angular:
                dimension = CreateAngularDimension(dimensionStyleId, plannedDimension);
                break;
            default:
                throw CreateDimensionFailure(
                    plannedDimension,
                    $"Unsupported dimension type '{plannedDimension.Type}'.");
        }

        dimension.Layer = plannedDimension.Layer;
        return dimension;
    }

    private static ObjectId ResolveDimensionStyleId(
        PlannedDimension plannedDimension,
        IReadOnlyDictionary<string, ObjectId> dimensionStyleIds)
    {
        var dimensionStyleName = string.IsNullOrWhiteSpace(plannedDimension.DimensionStyleName)
            ? CadDimensionStyleStandards.ResolveForDimensionType(plannedDimension.Type).Name
            : plannedDimension.DimensionStyleName;

        return dimensionStyleIds[dimensionStyleName];
    }

    private static RotatedDimension CreateLinearDimension(ObjectId dimensionStyleId, PlannedDimension plannedDimension)
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
            dimensionStyleId);
    }

    private static AlignedDimension CreateAlignedDimension(ObjectId dimensionStyleId, PlannedDimension plannedDimension)
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
            dimensionStyleId);
    }

    private static RadialDimension CreateRadiusDimension(
        ObjectId dimensionStyleId,
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
            dimensionStyleId);
    }

    private static DiametricDimension CreateDiameterDimension(
        ObjectId dimensionStyleId,
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

        return new DiametricDimension(left, right, 0, plannedDimension.Text, dimensionStyleId);
    }

    private static Point3AngularDimension CreateAngularDimension(ObjectId dimensionStyleId, PlannedDimension plannedDimension)
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
            dimensionStyleId);
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
