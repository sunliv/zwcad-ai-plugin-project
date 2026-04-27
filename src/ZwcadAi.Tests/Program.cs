using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using ZwcadAi.AiService;
using ZwcadAi.Core;
using ZwcadAi.Renderer;

namespace ZwcadAi.Tests;

public static class Program
{
    public static int Main()
    {
        var tests = new List<(string Name, Action Execute)>
        {
            ("Core uses the locked MVP domain", CoreUsesLockedMvpDomain),
            ("AI request defaults to mechanical plate", AiRequestDefaultsToMechanicalPlate),
            ("Fixed rectangular plate sample matches P1-03 geometry", FixedRectangularPlateSampleMatchesP103Geometry),
            ("DrawingSpec schema accepts valid example files", DrawingSpecSchemaAcceptsValidExampleFiles),
            ("Basic entities combo example validates and plans P3-01 entities", BasicEntitiesComboExampleValidatesAndPlansP301Entities),
            ("DrawingSpec schema rejects invalid example files", DrawingSpecSchemaRejectsInvalidExampleFiles),
            ("DrawingSpec schema rejects unsupported entity type", DrawingSpecSchemaRejectsUnsupportedEntityType),
            ("DrawingSpec schema rejects missing required fields", DrawingSpecSchemaRejectsMissingRequiredFields),
            ("DrawingSpec schema issue paths locate the failed field", DrawingSpecSchemaIssuePathsLocateFailedField),
            ("DrawingSpec schema rejects object model layer color zero", DrawingSpecSchemaRejectsObjectModelLayerColorZero),
            ("DrawingSpec business rules accept the fixed P1-03 sample", DrawingSpecBusinessRulesAcceptFixedP103Sample),
            ("DrawingSpec business rules reject unsupported layers", DrawingSpecBusinessRulesRejectUnsupportedLayers),
            ("DrawingSpec business rules reject oversized coordinates", DrawingSpecBusinessRulesRejectOversizedCoordinates),
            ("DrawingSpec business rules reject entity count over limit", DrawingSpecBusinessRulesRejectEntityCountOverLimit),
            ("DrawingSpec business rules reject incomplete angular dimensions", DrawingSpecBusinessRulesRejectIncompleteAngularDimensions),
            ("Renderer plans P1-03 plate entities on standard layers", RendererPlansP103PlateEntitiesOnStandardLayers),
            ("Renderer maps every P3-01 basic entity id", RendererMapsEveryP301BasicEntityId),
            ("Renderer plans P3-02 aligned and angular dimensions", RendererPlansP302AlignedAndAngularDimensions),
            ("Renderer plans P3-02 hole array centerlines", RendererPlansP302HoleArrayCenterlines),
            ("Renderer dimension failures locate stable dimension ids", RendererDimensionFailuresLocateStableDimensionIds),
            ("Renderer failures locate stable entity ids", RendererFailuresLocateStableEntityIds),
            ("Renderer rejects specs missing production layers", RendererRejectsSpecsMissingProductionLayers),
            ("Renderer result preserves spec-to-CAD mapping", RendererResultPreservesMapping),
            ("Project references follow architecture boundaries", ProjectReferencesFollowArchitectureBoundaries),
            ("Core project has no ZWCAD runtime references", CoreProjectHasNoZwcadRuntimeReferences),
            ("Plugin references ZWCAD 2025 managed assemblies", PluginReferencesZwcad2025ManagedAssemblies),
            ("Plugin registers AIDRAW command", PluginRegistersAiDrawCommand),
            ("Plugin AIDRAW uses fixed POC sample and transaction writer", PluginAiDrawUsesFixedPocSampleAndTransactionWriter),
            ("Plugin registers AIEXPORT command", PluginRegistersAiExportCommand),
            ("AIEXPORT saves a DWG copy without saving the active drawing", PluginAiExportSavesDwgCopyWithoutSavingActiveDrawing),
            ("AIEXPORT covers the PDF plot-to-file path", PluginAiExportCoversPdfPlotToFilePath),
            ("Plugin writer supports P3-01 basic entity dispatch", PluginWriterSupportsP301BasicEntityDispatch),
            ("Plugin writer supports P3-02 dimensions and center marks", PluginWriterSupportsP302DimensionsAndCenterMarks)
        };

        foreach (var test in tests)
        {
            test.Execute();
            Console.WriteLine($"PASS {test.Name}");
        }

        Console.WriteLine($"{tests.Count} tests passed.");
        return 0;
    }

    private static void CoreUsesLockedMvpDomain()
    {
        AssertEqual("mechanical_plate", DrawingDomain.MechanicalPlate);
    }

    private static void AiRequestDefaultsToMechanicalPlate()
    {
        var request = new AiDrawingSpecRequest();

        AssertEqual(DrawingDomain.MechanicalPlate, request.Domain);
        AssertEqual("mm", request.Units);
        AssertEqual("1.0", request.DrawingSpecVersion);
    }

    private static void FixedRectangularPlateSampleMatchesP103Geometry()
    {
        var spec = RectangularPlateSample.Create();

        AssertEqual("1.0", spec.DrawingSpecVersion);
        AssertEqual("mm", spec.Units);
        AssertSequenceEqual(new[] { "OUTLINE", "CENTER", "DIM" }, spec.Layers.Select(layer => layer.Name).ToArray());

        var outline = spec.Entities.Single(entity => entity.Id == "outer-profile");
        AssertEqual(EntityTypes.Polyline, outline.Type);
        AssertEqual("OUTLINE", outline.Layer);
        Assert(outline.Closed, "Outer profile must be closed.");
        AssertEqual(4, outline.Points.Count);
        AssertPoint(0, 0, outline.Points[0]);
        AssertPoint(100, 0, outline.Points[1]);
        AssertPoint(100, 60, outline.Points[2]);
        AssertPoint(0, 60, outline.Points[3]);

        var hole = spec.Entities.Single(entity => entity.Id == "hole-1");
        AssertEqual(EntityTypes.Circle, hole.Type);
        AssertEqual("OUTLINE", hole.Layer);
        AssertPoint(30, 30, hole.Center);
        AssertEqual(6d, hole.Radius);

        var centerMark = spec.Entities.Single(entity => entity.Id == "hole-1-center");
        AssertEqual(EntityTypes.CenterMark, centerMark.Type);
        AssertEqual("CENTER", centerMark.Layer);
        AssertPoint(30, 30, centerMark.Center);
        AssertEqual(10d, centerMark.Size);
    }

    private static void DrawingSpecSchemaAcceptsValidExampleFiles()
    {
        var examplesDirectory = Path.Combine(FindRepositoryRoot(), "examples");
        var allExamplePaths = Directory
            .GetFiles(examplesDirectory, "*.example.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var validExamplePaths = Directory
            .GetFiles(examplesDirectory, "*.example.json")
            .Where(path => Path.GetFileName(path).IndexOf(".invalid.", StringComparison.OrdinalIgnoreCase) < 0)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert(allExamplePaths.Length >= 6, "P2 must keep the existing example and add at least five protocol examples.");

        foreach (var examplePath in validExamplePaths)
        {
            var result = DrawingSpecValidator.ValidateSchemaJson(File.ReadAllText(examplePath));

            Assert(
                result.IsValid,
                $"Example '{Path.GetFileName(examplePath)}' must pass schema validation: {FormatIssues(result.Issues)}");
        }
    }

    private static void BasicEntitiesComboExampleValidatesAndPlansP301Entities()
    {
        var json = ReadExampleJson("basic-entities-combo.example.json");
        var schemaResult = DrawingSpecValidator.ValidateSchemaJson(json);
        Assert(
            schemaResult.IsValid,
            $"basic-entities-combo.example.json must pass schema validation: {FormatIssues(schemaResult.Issues)}");

        var spec = ReadExampleSpec("basic-entities-combo.example.json");
        var businessResult = DrawingSpecValidator.ValidateBusinessRules(spec);
        Assert(
            businessResult.IsValid,
            $"basic-entities-combo.example.json must pass business validation: {FormatIssues(businessResult.Issues)}");

        var plan = new DrawingSpecPlanRenderer().CreatePlan(spec);
        Assert(
            plan.Validation.IsValid,
            $"basic-entities-combo.example.json must produce a valid render plan: {FormatIssues(plan.Validation.Issues)}");

        var textLayer = plan.Layers.Single(layer => layer.Name == "TEXT");
        AssertEqual(2, textLayer.Color);
        AssertEqual("Continuous", textLayer.LineType);
        AssertEqual(0.18d, textLayer.LineWeight);

        foreach (var expected in P301BasicEntityKinds())
        {
            var planned = plan.Entities.Single(entity => entity.SpecEntityId == expected.Key);
            AssertEqual(expected.Value, planned.Kind);
            AssertEqual(expected.Key, planned.SourceEntityId);
        }

        var line = plan.Entities.Single(entity => entity.SpecEntityId == "baseline");
        AssertPoint(0, 0, line.Start);
        AssertPoint(90, 0, line.End);

        var polyline = plan.Entities.Single(entity => entity.SpecEntityId == "open-profile");
        Assert(!polyline.Closed, "Open profile must remain open.");
        AssertEqual(4, polyline.Points.Count);
        AssertPoint(20, 25, polyline.Points[1]);

        var circle = plan.Entities.Single(entity => entity.SpecEntityId == "reference-circle");
        AssertPoint(45, 12, circle.Center);
        AssertEqual(8d, circle.Radius);

        var arc = plan.Entities.Single(entity => entity.SpecEntityId == "relief-arc");
        AssertPoint(45, 25, arc.Center);
        AssertEqual(14d, arc.Radius);
        AssertEqual(180d, arc.StartAngle);
        AssertEqual(360d, arc.EndAngle);

        var text = plan.Entities.Single(entity => entity.SpecEntityId == "note-1");
        AssertPoint(0, 38, text.Position);
        AssertEqual("BASIC ENTITY SAMPLE", text.Value);
        AssertEqual(3.5d, text.Height);
        AssertEqual(0d, text.Rotation);

        var mtext = plan.Entities.Single(entity => entity.SpecEntityId == "note-mtext-1");
        AssertPoint(0, 44, mtext.Position);
        AssertEqual("Line, polyline, circle, arc, text, and mtext", mtext.Value);
        AssertEqual(2.5d, mtext.Height);

        AssertEqual(2, plan.Dimensions.Count);
    }

    private static void DrawingSpecSchemaRejectsInvalidExampleFiles()
    {
        var examplesDirectory = Path.Combine(FindRepositoryRoot(), "examples");
        var invalidExamplePaths = Directory
            .GetFiles(examplesDirectory, "*.invalid.example.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert(invalidExamplePaths.Length >= 1, "P2 must include at least one invalid DrawingSpec example.");

        foreach (var examplePath in invalidExamplePaths)
        {
            var result = DrawingSpecValidator.ValidateSchemaJson(File.ReadAllText(examplePath));

            Assert(
                !result.IsValid,
                $"Invalid example '{Path.GetFileName(examplePath)}' must fail schema validation.");
        }
    }

    private static void DrawingSpecSchemaRejectsUnsupportedEntityType()
    {
        var json = MinimalSpecJson("""
          {
            "id": "bad-entity",
            "type": "spline",
            "layer": "OUTLINE",
            "start": [0, 0],
            "end": [10, 0]
          }
        """);

        var result = DrawingSpecValidator.ValidateSchemaJson(json);

        Assert(!result.IsValid, "Unsupported entity type must fail schema validation.");
        Assert(
            result.Issues.Any(issue => issue.Code == "unsupported_entity_type" && issue.Path == "$.entities[0].type"),
            $"Unsupported entity type must report $.entities[0].type, got: {FormatIssues(result.Issues)}");
    }

    private static void DrawingSpecSchemaRejectsMissingRequiredFields()
    {
        var json = MinimalSpecJson("""
          {
            "id": "missing-layer-line",
            "type": "line",
            "start": [0, 0],
            "end": [10, 0]
          }
        """);

        var result = DrawingSpecValidator.ValidateSchemaJson(json);

        Assert(!result.IsValid, "Missing entity layer must fail schema validation.");
        Assert(
            result.Issues.Any(issue => issue.Code == "missing_required" && issue.Path == "$.entities[0].layer"),
            $"Missing layer must report $.entities[0].layer, got: {FormatIssues(result.Issues)}");
    }

    private static void DrawingSpecSchemaIssuePathsLocateFailedField()
    {
        var json = MinimalSpecJson("""
          {
            "id": "bad-point",
            "type": "circle",
            "layer": "OUTLINE",
            "center": { "x": 5, "y": 5 },
            "radius": 2
          }
        """);

        var result = DrawingSpecValidator.ValidateSchemaJson(json);

        Assert(!result.IsValid, "Object-shaped point must fail because point2d wire format is [x, y].");
        Assert(
            result.Issues.Any(issue => issue.Path == "$.entities[0].center"),
            $"Point wire-format failure must report $.entities[0].center, got: {FormatIssues(result.Issues)}");
    }

    private static void DrawingSpecSchemaRejectsObjectModelLayerColorZero()
    {
        var spec = RectangularPlateSample.Create();
        spec.Layers = new[]
        {
            new LayerSpec
            {
                Name = CadLayerNames.Outline,
                Color = 0,
                LineType = "Continuous",
                LineWeight = 0.35
            }
        };

        var result = DrawingSpecValidator.ValidateSchema(spec);

        Assert(!result.IsValid, "Layer color 0 must fail object-model schema validation to match the JSON schema.");
        Assert(
            result.Issues.Any(issue => issue.Code == "invalid_value" && issue.Path == "$.layers[0].color"),
            $"Layer color 0 must report $.layers[0].color, got: {FormatIssues(result.Issues)}");
    }

    private static void DrawingSpecBusinessRulesAcceptFixedP103Sample()
    {
        var result = DrawingSpecValidator.ValidateBusinessRules(RectangularPlateSample.Create());

        Assert(result.IsValid, $"Fixed P1-03 sample must satisfy P2 business rules: {FormatIssues(result.Issues)}");
    }

    private static void DrawingSpecBusinessRulesRejectUnsupportedLayers()
    {
        var spec = RectangularPlateSample.Create();
        spec.Layers = spec.Layers.Concat(new[]
        {
            new LayerSpec
            {
                Name = "BAD",
                Color = 6,
                LineType = "Continuous",
                LineWeight = 0.18
            }
        }).ToArray();

        var result = DrawingSpecValidator.ValidateBusinessRules(spec);

        Assert(!result.IsValid, "Layer names outside enterprise-default-v1 must fail business validation.");
        Assert(
            result.Issues.Any(issue => issue.Code == "unsupported_layer" && issue.Path == "$.layers[BAD].name"),
            $"Unsupported layer must report $.layers[BAD].name, got: {FormatIssues(result.Issues)}");
    }

    private static void DrawingSpecBusinessRulesRejectOversizedCoordinates()
    {
        var spec = RectangularPlateSample.Create();
        spec.Entities = spec.Entities.Concat(new[]
        {
            new EntitySpec
            {
                Id = "unsafe-line",
                Type = EntityTypes.Line,
                Layer = CadLayerNames.Outline,
                Start = new DrawingPoint(0, 0),
                End = new DrawingPoint(100001, 0)
            }
        }).ToArray();

        var result = DrawingSpecValidator.ValidateBusinessRules(spec);

        Assert(!result.IsValid, "Coordinates outside the configured P2 boundary must fail business validation.");
        Assert(
            result.Issues.Any(issue => issue.Code == "coordinate_out_of_range" && issue.Path == "$.entities[unsafe-line].end"),
            $"Oversized coordinate must report $.entities[unsafe-line].end, got: {FormatIssues(result.Issues)}");
    }

    private static void DrawingSpecBusinessRulesRejectEntityCountOverLimit()
    {
        var spec = RectangularPlateSample.Create();
        spec.Entities = Enumerable
            .Range(0, DrawingSpecBusinessRuleLimits.DefaultMaxEntities + 1)
            .Select(index => new EntitySpec
            {
                Id = $"line-{index}",
                Type = EntityTypes.Line,
                Layer = CadLayerNames.Outline,
                Start = new DrawingPoint(index, 0),
                End = new DrawingPoint(index, 10)
            })
            .ToArray();

        var result = DrawingSpecValidator.ValidateBusinessRules(spec);

        Assert(!result.IsValid, "Entity count over the configured P2 limit must fail business validation.");
        Assert(
            result.Issues.Any(issue => issue.Code == "entity_count_exceeded" && issue.Path == "$.entities"),
            $"Entity count failures must report $.entities, got: {FormatIssues(result.Issues)}");
    }

    private static void DrawingSpecBusinessRulesRejectIncompleteAngularDimensions()
    {
        var spec = RectangularPlateSample.Create();
        spec.Dimensions = spec.Dimensions.Concat(new[]
        {
            new DimensionSpec
            {
                Id = "dim-angle-missing-geometry",
                Type = DimensionTypes.Angular,
                Layer = CadLayerNames.Dimension
            }
        }).ToArray();

        var result = DrawingSpecValidator.ValidateBusinessRules(spec);

        Assert(!result.IsValid, "Angular dimensions without center/from/to/offset must fail business validation.");
        Assert(
            result.Issues.Any(issue => issue.Code == "missing_dimension_geometry" && issue.Path == "$.dimensions[dim-angle-missing-geometry]"),
            $"Incomplete angular dimension must report $.dimensions[dim-angle-missing-geometry], got: {FormatIssues(result.Issues)}");
    }

    private static void RendererPlansP103PlateEntitiesOnStandardLayers()
    {
        var spec = RectangularPlateSample.Create();
        var renderer = new DrawingSpecPlanRenderer();

        var plan = renderer.CreatePlan(spec);

        Assert(plan.Validation.IsValid, "P1-03 sample should be valid for render planning.");
        AssertEqual(3, plan.Layers.Count);
        AssertEqual(1, plan.Entities.Count(entity => entity.Kind == PlannedEntityKind.Polyline));
        AssertEqual(1, plan.Entities.Count(entity => entity.Kind == PlannedEntityKind.Circle));
        AssertEqual(2, plan.Entities.Count(entity => entity.Kind == PlannedEntityKind.CenterLine));
        AssertEqual(3, plan.Dimensions.Count);

        var circle = plan.Entities.Single(entity => entity.SpecEntityId == "hole-1");
        AssertEqual("OUTLINE", circle.Layer);
        AssertPoint(30, 30, circle.Center);
        AssertEqual(6d, circle.Radius);

        var centerLines = plan.Entities.Where(entity => entity.SourceEntityId == "hole-1-center").ToArray();
        Assert(centerLines.All(entity => entity.Layer == "CENTER"), "Center mark lines must be on CENTER layer.");
        Assert(centerLines.Any(entity => PointsEqual(new DrawingPoint(20, 30), entity.Start) && PointsEqual(new DrawingPoint(40, 30), entity.End)),
            "Center mark must include the horizontal centerline.");
        Assert(centerLines.Any(entity => PointsEqual(new DrawingPoint(30, 20), entity.Start) && PointsEqual(new DrawingPoint(30, 40), entity.End)),
            "Center mark must include the vertical centerline.");

        Assert(plan.Dimensions.All(dimension => dimension.Layer == "DIM"), "All dimensions must be on DIM layer.");
        AssertEqual("dim-hole-dia", plan.Dimensions.Single(dimension => dimension.Type == DimensionTypes.Diameter).SpecDimensionId);
    }

    private static void RendererMapsEveryP301BasicEntityId()
    {
        var spec = ReadExampleSpec("basic-entities-combo.example.json");
        var renderer = new DrawingSpecPlanRenderer();

        var result = renderer.Render(spec, new RenderContext(spec.Metadata.RequestId, "enterprise-default-v1"));

        Assert(result.Success, "P3-01 basic entity combo should render through the deterministic renderer stub.");
        Assert(result.Validation.IsValid, $"Render validation should be valid: {FormatIssues(result.Validation.Issues)}");

        foreach (var specEntityId in P301BasicEntityKinds().Keys)
        {
            var rendered = result.Entities.Single(entity => entity.SpecEntityId == specEntityId);
            AssertEqual($"planned:{specEntityId}", rendered.CadObjectId);
        }
    }

    private static void RendererPlansP302AlignedAndAngularDimensions()
    {
        var json = ReadExampleJson("annotation-angular-aligned.example.json");
        var schemaResult = DrawingSpecValidator.ValidateSchemaJson(json);
        Assert(
            schemaResult.IsValid,
            $"annotation-angular-aligned.example.json must pass schema validation: {FormatIssues(schemaResult.Issues)}");

        var spec = ReadExampleSpec("annotation-angular-aligned.example.json");
        var businessResult = DrawingSpecValidator.ValidateBusinessRules(spec);
        Assert(
            businessResult.IsValid,
            $"annotation-angular-aligned.example.json must pass business validation: {FormatIssues(businessResult.Issues)}");

        var plan = new DrawingSpecPlanRenderer().CreatePlan(spec);
        Assert(
            plan.Validation.IsValid,
            $"annotation-angular-aligned.example.json must produce a valid render plan: {FormatIssues(plan.Validation.Issues)}");

        AssertEqual(2, plan.Dimensions.Count);

        var aligned = plan.Dimensions.Single(dimension => dimension.SpecDimensionId == "dim-angled-edge");
        AssertEqual(DimensionTypes.Aligned, aligned.Type);
        AssertEqual(CadLayerNames.Dimension, aligned.Layer);
        AssertPoint(0, 0, aligned.From);
        AssertPoint(40, 30, aligned.To);
        AssertPoint(8, 8, aligned.Offset);
        AssertEqual("50", aligned.Text);

        var angular = plan.Dimensions.Single(dimension => dimension.SpecDimensionId == "dim-angle-between-edges");
        AssertEqual(DimensionTypes.Angular, angular.Type);
        AssertEqual(CadLayerNames.Dimension, angular.Layer);
        AssertPoint(0, 0, angular.Center);
        AssertPoint(50, 0, angular.From);
        AssertPoint(40, 30, angular.To);
        AssertPoint(18, 14, angular.Offset);
        AssertEqual("36.9%%d", angular.Text);

        var result = new DrawingSpecPlanRenderer().Render(spec, new RenderContext(spec.Metadata.RequestId, "enterprise-default-v1"));
        Assert(result.Success, "P3-02 annotation example should render through the deterministic renderer stub.");
        Assert(
            result.Entities.Any(entity => entity.SpecEntityId == "dim-angled-edge")
                && result.Entities.Any(entity => entity.SpecEntityId == "dim-angle-between-edges"),
            "P3-02 renderer result must preserve aligned and angular dimension ids.");
    }

    private static void RendererPlansP302HoleArrayCenterlines()
    {
        var spec = ReadExampleSpec("hole-array-centerlines.example.json");
        var plan = new DrawingSpecPlanRenderer().CreatePlan(spec);

        Assert(
            plan.Validation.IsValid,
            $"hole-array-centerlines.example.json must produce a valid render plan: {FormatIssues(plan.Validation.Issues)}");

        AssertEqual(3, plan.Entities.Count(entity => entity.Kind == PlannedEntityKind.Circle));
        AssertEqual(7, plan.Entities.Count(entity => entity.Kind == PlannedEntityKind.CenterLine));

        var explicitArrayCenterline = plan.Entities.Single(entity => entity.SpecEntityId == "array-centerline");
        AssertEqual(PlannedEntityKind.CenterLine, explicitArrayCenterline.Kind);
        AssertEqual("array-centerline", explicitArrayCenterline.SourceEntityId);
        AssertEqual(CadLayerNames.Center, explicitArrayCenterline.Layer);
        AssertPoint(20, 35, explicitArrayCenterline.Start);
        AssertPoint(130, 35, explicitArrayCenterline.End);
        AssertEqual(110d, CenterLineLength(explicitArrayCenterline));

        foreach (var holeCenterId in new[] { "hole-1-center", "hole-2-center", "hole-3-center" })
        {
            var centerLines = plan.Entities
                .Where(entity => entity.SourceEntityId == holeCenterId)
                .OrderBy(entity => entity.SpecEntityId, StringComparer.Ordinal)
                .ToArray();

            AssertEqual(2, centerLines.Length);
            Assert(centerLines.All(entity => entity.Layer == CadLayerNames.Center), $"{holeCenterId} centerlines must stay on CENTER.");
            Assert(centerLines.Any(entity => entity.SpecEntityId == $"{holeCenterId}-horizontal"), $"{holeCenterId} must map a horizontal derived id.");
            Assert(centerLines.Any(entity => entity.SpecEntityId == $"{holeCenterId}-vertical"), $"{holeCenterId} must map a vertical derived id.");
            Assert(
                centerLines.All(entity => CenterLineLength(entity) == 22d),
                $"{holeCenterId} centerlines must expand size 11 to total length 22.");
        }

        AssertEqual(3, plan.Dimensions.Count);

        var pitch1 = plan.Dimensions.Single(dimension => dimension.SpecDimensionId == "dim-array-pitch-1");
        AssertEqual(DimensionTypes.Linear, pitch1.Type);
        AssertPoint(35, 35, pitch1.From);
        AssertPoint(75, 35, pitch1.To);
        AssertPoint(0, -18, pitch1.Offset);
        AssertEqual("40", pitch1.Text);

        var pitch2 = plan.Dimensions.Single(dimension => dimension.SpecDimensionId == "dim-array-pitch-2");
        AssertEqual(DimensionTypes.Linear, pitch2.Type);
        AssertPoint(75, 35, pitch2.From);
        AssertPoint(115, 35, pitch2.To);
        AssertPoint(0, -18, pitch2.Offset);
        AssertEqual("40", pitch2.Text);

        var diameter = plan.Dimensions.Single(dimension => dimension.Type == DimensionTypes.Diameter);
        AssertEqual("dim-hole-dia", diameter.SpecDimensionId);
        AssertEqual("hole-1", diameter.TargetEntityId);
        AssertEqual("3X %%c12", diameter.Text);

        var result = new DrawingSpecPlanRenderer().Render(spec, new RenderContext(spec.Metadata.RequestId, "enterprise-default-v1"));
        Assert(result.Success, "P3-02 hole array example should render through the deterministic renderer stub.");
        Assert(
            result.Entities.Any(entity => entity.SpecEntityId == "hole-1-center-horizontal")
                && result.Entities.Any(entity => entity.SpecEntityId == "hole-3-center-vertical")
                && result.Entities.Any(entity => entity.SpecEntityId == "array-centerline")
                && result.Entities.Any(entity => entity.SpecEntityId == "dim-hole-dia"),
            "P3-02 renderer result must preserve centerline and dimension ids.");
    }

    private static void RendererDimensionFailuresLocateStableDimensionIds()
    {
        var spec = ReadExampleSpec("annotation-angular-aligned.example.json");
        spec.Dimensions.Single(dimension => dimension.Id == "dim-angle-between-edges").Offset = null;
        var renderer = new DrawingSpecPlanRenderer();

        var plan = renderer.CreatePlan(spec);

        Assert(!plan.Validation.IsValid, "Invalid angular dimension geometry must fail render planning.");
        Assert(
            plan.Validation.Issues.Any(issue =>
                issue.Code == "missing_dimension_geometry" && issue.Path == "$.dimensions[dim-angle-between-edges]"),
            $"Angular dimension failure must locate dim-angle-between-edges, got: {FormatIssues(plan.Validation.Issues)}");
    }

    private static void RendererFailuresLocateStableEntityIds()
    {
        var spec = ReadExampleSpec("basic-entities-combo.example.json");
        spec.Entities.Single(entity => entity.Id == "relief-arc").Radius = 0;
        var renderer = new DrawingSpecPlanRenderer();

        var plan = renderer.CreatePlan(spec);

        Assert(!plan.Validation.IsValid, "Invalid arc geometry must fail render planning.");
        Assert(
            plan.Validation.Issues.Any(issue =>
                issue.Code == "invalid_arc_geometry" && issue.Path == "$.entities[relief-arc]"),
            $"Arc render planning failure must locate relief-arc, got: {FormatIssues(plan.Validation.Issues)}");
    }

    private static void RendererRejectsSpecsMissingProductionLayers()
    {
        var spec = new DrawingSpec
        {
            DrawingSpecVersion = "1.0",
            Units = "mm",
            Metadata = new DrawingMetadata
            {
                Domain = DrawingDomain.MechanicalPlate,
                CreatedBy = "test",
                RequestId = "renderer-missing-production-layers"
            },
            Layers = new[]
            {
                new LayerSpec
                {
                    Name = CadLayerNames.Outline,
                    Color = 7,
                    LineType = "Continuous",
                    LineWeight = 0.35
                }
            },
            Entities = new[]
            {
                new EntitySpec
                {
                    Id = "line-1",
                    Type = EntityTypes.Line,
                    Layer = CadLayerNames.Outline,
                    Start = new DrawingPoint(0, 0),
                    End = new DrawingPoint(10, 0)
                }
            },
            Dimensions = Array.Empty<DimensionSpec>(),
            Clarifications = Array.Empty<string>()
        };

        var renderer = new DrawingSpecPlanRenderer();
        var plan = renderer.CreatePlan(spec);

        Assert(!plan.Validation.IsValid, "Renderer must enforce production DrawingSpec business validation before planning.");
        Assert(
            plan.Validation.Issues.Any(issue => issue.Code == "missing_required_layer" && issue.Path == "$.layers[CENTER]"),
            $"Renderer should surface missing CENTER layer from business validation, got: {FormatIssues(plan.Validation.Issues)}");
    }

    private static void RendererResultPreservesMapping()
    {
        var result = new RenderResult(
            success: true,
            entities: new[] { new RenderedEntity("hole-1", "cad-object-1") },
            validation: ValidationResult.Success());

        Assert(result.Success, "Render result should be successful.");
        Assert(result.Validation.IsValid, "Validation should be valid.");
        AssertEqual("hole-1", result.Entities.Single().SpecEntityId);
        AssertEqual("cad-object-1", result.Entities.Single().CadObjectId);
    }

    private static void ProjectReferencesFollowArchitectureBoundaries()
    {
        var root = FindRepositoryRoot();

        var coreRefs = ReadProjectReferences(Path.Combine(root, "src", "ZwcadAi.Core", "ZwcadAi.Core.csproj"));
        var rendererRefs = ReadProjectReferences(Path.Combine(root, "src", "ZwcadAi.Renderer", "ZwcadAi.Renderer.csproj"));
        var aiRefs = ReadProjectReferences(Path.Combine(root, "src", "ZwcadAi.AiService", "ZwcadAi.AiService.csproj"));
        var pluginRefs = ReadProjectReferences(Path.Combine(root, "src", "ZwcadAiPlugin", "ZwcadAiPlugin.csproj"));

        AssertEqual(0, coreRefs.Count);
        AssertSequenceEqual(new[] { "ZwcadAi.Core.csproj" }, rendererRefs);
        AssertSequenceEqual(new[] { "ZwcadAi.Core.csproj" }, aiRefs);
        AssertSequenceEqual(
            new[] { "ZwcadAi.Core.csproj", "ZwcadAi.Renderer.csproj", "ZwcadAi.AiService.csproj" },
            pluginRefs);
    }

    private static void CoreProjectHasNoZwcadRuntimeReferences()
    {
        var root = FindRepositoryRoot();
        var projectPath = Path.Combine(root, "src", "ZwcadAi.Core", "ZwcadAi.Core.csproj");
        var document = XDocument.Load(projectPath);

        var references = document
            .Descendants("Reference")
            .Select(reference => (string?)reference.Attribute("Include") ?? string.Empty)
            .ToArray();

        var forbiddenReference = references.FirstOrDefault(IsZwcadRuntimeReference);
        Assert(
            forbiddenReference == null,
            $"Core project must not reference ZWCAD runtime assemblies, found '{forbiddenReference}'.");
    }

    private static void PluginReferencesZwcad2025ManagedAssemblies()
    {
        var root = FindRepositoryRoot();
        var projectPath = Path.Combine(root, "src", "ZwcadAiPlugin", "ZwcadAiPlugin.csproj");
        var document = XDocument.Load(projectPath);

        var references = document
            .Descendants("Reference")
            .ToDictionary(
                reference => ((string?)reference.Attribute("Include") ?? string.Empty).Split(',')[0],
                StringComparer.OrdinalIgnoreCase);

        Assert(references.ContainsKey("ZwManaged"), "Plugin must reference ZwManaged.dll.");
        Assert(references.ContainsKey("ZwDatabaseMgd"), "Plugin must reference ZwDatabaseMgd.dll.");

        AssertZwcadReference(references["ZwManaged"], "ZwManaged.dll");
        AssertZwcadReference(references["ZwDatabaseMgd"], "ZwDatabaseMgd.dll");
    }

    private static void PluginRegistersAiDrawCommand()
    {
        var source = ReadPluginSource();

        Assert(
            source.Contains("IExtensionApplication", StringComparison.Ordinal),
            "Plugin must expose a ZWCAD extension application entry point.");
        Assert(
            source.Contains("CommandMethod(PluginCommandCatalog.AiDraw", StringComparison.Ordinal),
            "Plugin must register AIDRAW through CommandMethod.");
        Assert(
            source.Contains("readyForCadLoad: true", StringComparison.Ordinal),
            "Plugin runtime status must report CAD load readiness.");
    }

    private static void PluginAiDrawUsesFixedPocSampleAndTransactionWriter()
    {
        var source = ReadPluginSource();

        Assert(
            source.Contains("RectangularPlateSample.Create()", StringComparison.Ordinal),
            "AIDRAW must load the fixed P1-03 rectangular plate sample before AI integration exists.");
        Assert(
            source.Contains("ZwcadDrawingWriter", StringComparison.Ordinal),
            "AIDRAW must use the ZWCAD transaction writer for the POC render.");
        Assert(
            source.Contains("StartTransaction()", StringComparison.Ordinal),
            "P1-03 CAD writes must be wrapped in a transaction for rollback on failure.");
    }

    private static void PluginRegistersAiExportCommand()
    {
        var source = ReadPluginSource();

        Assert(
            source.Contains("CommandMethod(PluginCommandCatalog.AiExport", StringComparison.Ordinal),
            "Plugin must register AIEXPORT through CommandMethod.");
        Assert(
            source.Contains("ExportActiveDocument()", StringComparison.Ordinal),
            "AIEXPORT must call the export service entry point.");
    }

    private static void PluginAiExportSavesDwgCopyWithoutSavingActiveDrawing()
    {
        var source = ReadPluginSource();

        Assert(
            source.Contains("SaveDwgCopy(", StringComparison.Ordinal),
            "AIEXPORT must have an explicit DWG copy save step.");
        Assert(
            source.Contains("database.Wblock()", StringComparison.Ordinal),
            "AIEXPORT must create an independent DWG database copy before saving.");
        Assert(
            source.Contains("copy.SaveAs(dwgPath, DwgVersion.Current)", StringComparison.Ordinal),
            "AIEXPORT must save the copied database to a DWG output path using Database.SaveAs.");
        Assert(
            !source.Contains(".Save()", StringComparison.Ordinal),
            "AIEXPORT must not call Database.Save because the POC must not save over the active drawing.");
        Assert(
            source.Contains("AIEXPORT DWG copy:", StringComparison.Ordinal),
            "AIEXPORT must log the DWG copy output path.");
    }

    private static void PluginAiExportCoversPdfPlotToFilePath()
    {
        var source = ReadPluginSource();

        Assert(
            source.Contains("ZwSoft.ZwCAD.PlottingServices", StringComparison.Ordinal),
            "AIEXPORT must use the ZWCAD plotting services for the PDF export path.");
        Assert(
            source.Contains("PlotFactory.CreatePublishEngine()", StringComparison.Ordinal),
            "AIEXPORT must create a publish plot engine for PDF output.");
        Assert(
            source.Contains("BeginDocument(plotInfo, document.Name, null, 1, true, pdfPath)", StringComparison.Ordinal),
            "AIEXPORT must plot to a PDF file path instead of only plotting to a device.");
        Assert(
            source.Contains("AIEXPORT PDF export unavailable", StringComparison.Ordinal),
            "AIEXPORT must clearly log when the current ZWCAD environment cannot export PDF.");
    }

    private static void PluginWriterSupportsP301BasicEntityDispatch()
    {
        var source = ReadPluginSource();

        Assert(
            source.Contains("EntityFactories", StringComparison.Ordinal),
            "ZWCAD writer should use an explicit entity dispatch table instead of keeping P1 switch growth.");
        Assert(
            source.Contains("[PlannedEntityKind.Arc] = CreateArc", StringComparison.Ordinal),
            "ZWCAD writer must dispatch P3-01 arc entities.");
        Assert(
            source.Contains("[PlannedEntityKind.Text] = CreateText", StringComparison.Ordinal),
            "ZWCAD writer must dispatch P3-01 text entities.");
        Assert(
            source.Contains("[PlannedEntityKind.MText] = CreateMText", StringComparison.Ordinal),
            "ZWCAD writer must dispatch P3-01 mtext entities.");
        Assert(
            source.Contains("new Arc(", StringComparison.Ordinal)
                && source.Contains("ToRadians(plannedEntity.StartAngle)", StringComparison.Ordinal)
                && source.Contains("ToRadians(plannedEntity.EndAngle)", StringComparison.Ordinal),
            "Arc writer must create a CAD Arc and convert DrawingSpec degrees to CAD radians.");
        Assert(
            source.Contains("new DBText", StringComparison.Ordinal)
                && source.Contains("TextString = plannedEntity.Value", StringComparison.Ordinal),
            "Text writer must create a DBText from the planned text value.");
        Assert(
            source.Contains("new MText", StringComparison.Ordinal)
                && source.Contains("Contents = plannedEntity.Value", StringComparison.Ordinal),
            "MText writer must create an MText from the planned text value.");
        Assert(
            source.Contains("new RenderedEntity(plannedEntity.SpecEntityId, objectId.ToString())", StringComparison.Ordinal),
            "ZWCAD writer must preserve spec entity id to CAD object id mapping.");
        Assert(
            source.Contains("case DimensionTypes.Radius", StringComparison.Ordinal)
                && source.Contains("CreateRadiusDimension", StringComparison.Ordinal),
            "ZWCAD writer must support the radius dimension already present in basic-entities-combo.example.json.");
    }

    private static void PluginWriterSupportsP302DimensionsAndCenterMarks()
    {
        var source = ReadPluginSource();

        Assert(
            source.Contains("case DimensionTypes.Aligned", StringComparison.Ordinal)
                && source.Contains("CreateAlignedDimension", StringComparison.Ordinal),
            "ZWCAD writer must dispatch P3-02 aligned dimensions.");
        Assert(
            source.Contains("case DimensionTypes.Angular", StringComparison.Ordinal)
                && source.Contains("CreateAngularDimension", StringComparison.Ordinal),
            "ZWCAD writer must dispatch P3-02 angular dimensions.");
        Assert(
            source.Contains("new AlignedDimension(", StringComparison.Ordinal),
            "Aligned dimension writer must create a CAD AlignedDimension.");
        Assert(
            source.Contains("new Point3AngularDimension(", StringComparison.Ordinal),
            "Angular dimension writer must create a CAD Point3AngularDimension.");
        Assert(
            source.Contains("CreateDimensionFailure", StringComparison.Ordinal)
                && source.Contains("$.dimensions[", StringComparison.Ordinal)
                && source.Contains("plannedDimension.SpecDimensionId", StringComparison.Ordinal),
            "Dimension writer failures must locate stable DrawingSpec dimension ids.");
        Assert(
            source.Contains("[PlannedEntityKind.CenterLine] = CreateLine", StringComparison.Ordinal),
            "ZWCAD writer must render center mark expansions and explicit centerlines through line entities.");
    }

    private static IReadOnlyDictionary<string, PlannedEntityKind> P301BasicEntityKinds()
    {
        return new Dictionary<string, PlannedEntityKind>(StringComparer.Ordinal)
        {
            ["baseline"] = PlannedEntityKind.Line,
            ["open-profile"] = PlannedEntityKind.Polyline,
            ["reference-circle"] = PlannedEntityKind.Circle,
            ["relief-arc"] = PlannedEntityKind.Arc,
            ["note-1"] = PlannedEntityKind.Text,
            ["note-mtext-1"] = PlannedEntityKind.MText
        };
    }

    private static string ReadExampleJson(string fileName)
    {
        return File.ReadAllText(Path.Combine(FindRepositoryRoot(), "examples", fileName));
    }

    private static DrawingSpec ReadExampleSpec(string fileName)
    {
        using var document = JsonDocument.Parse(ReadExampleJson(fileName));
        var root = document.RootElement;

        return new DrawingSpec
        {
            DrawingSpecVersion = ReadString(root, "drawingSpecVersion"),
            Units = ReadString(root, "units"),
            Metadata = root.TryGetProperty("metadata", out var metadata)
                ? ReadMetadata(metadata)
                : new DrawingMetadata(),
            Layers = ReadLayers(root),
            Entities = ReadEntities(root),
            Dimensions = ReadDimensions(root),
            Clarifications = ReadStringArray(root, "clarifications")
        };
    }

    private static DrawingMetadata ReadMetadata(JsonElement metadata)
    {
        return new DrawingMetadata
        {
            Title = ReadString(metadata, "title"),
            Domain = ReadString(metadata, "domain"),
            Author = ReadString(metadata, "author"),
            CreatedBy = ReadString(metadata, "createdBy"),
            RequestId = ReadString(metadata, "requestId")
        };
    }

    private static IReadOnlyList<LayerSpec> ReadLayers(JsonElement root)
    {
        if (!root.TryGetProperty("layers", out var layers))
        {
            return Array.Empty<LayerSpec>();
        }

        return layers.EnumerateArray()
            .Select(layer => new LayerSpec
            {
                Name = ReadString(layer, "name"),
                Color = ReadInt(layer, "color"),
                LineType = ReadString(layer, "lineType"),
                LineWeight = ReadDouble(layer, "lineWeight")
            })
            .ToArray();
    }

    private static IReadOnlyList<EntitySpec> ReadEntities(JsonElement root)
    {
        if (!root.TryGetProperty("entities", out var entities))
        {
            return Array.Empty<EntitySpec>();
        }

        return entities.EnumerateArray()
            .Select(entity => new EntitySpec
            {
                Id = ReadString(entity, "id"),
                Type = ReadString(entity, "type"),
                Layer = ReadString(entity, "layer"),
                Closed = ReadBoolean(entity, "closed"),
                Points = ReadPointArray(entity, "points"),
                Start = ReadPoint(entity, "start"),
                End = ReadPoint(entity, "end"),
                Center = ReadPoint(entity, "center"),
                Position = ReadPoint(entity, "position"),
                Radius = ReadDouble(entity, "radius"),
                Size = ReadDouble(entity, "size"),
                StartAngle = ReadDouble(entity, "startAngle"),
                EndAngle = ReadDouble(entity, "endAngle"),
                Value = ReadString(entity, "value"),
                Height = ReadDouble(entity, "height"),
                Rotation = ReadDouble(entity, "rotation")
            })
            .ToArray();
    }

    private static IReadOnlyList<DimensionSpec> ReadDimensions(JsonElement root)
    {
        if (!root.TryGetProperty("dimensions", out var dimensions))
        {
            return Array.Empty<DimensionSpec>();
        }

        return dimensions.EnumerateArray()
            .Select(dimension => new DimensionSpec
            {
                Id = ReadString(dimension, "id"),
                Type = ReadString(dimension, "type"),
                Layer = ReadString(dimension, "layer"),
                From = ReadPoint(dimension, "from"),
                To = ReadPoint(dimension, "to"),
                Center = ReadPoint(dimension, "center"),
                TargetEntityId = ReadString(dimension, "targetEntityId"),
                Offset = ReadPoint(dimension, "offset"),
                Text = ReadString(dimension, "text")
            })
            .ToArray();
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var values))
        {
            return Array.Empty<string>();
        }

        return values.EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToArray();
    }

    private static IReadOnlyList<DrawingPoint> ReadPointArray(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var values))
        {
            return Array.Empty<DrawingPoint>();
        }

        return values.EnumerateArray()
            .Select(ReadPoint)
            .ToArray();
    }

    private static DrawingPoint? ReadPoint(JsonElement obj, string propertyName)
    {
        return obj.TryGetProperty(propertyName, out var point) ? ReadPoint(point) : null;
    }

    private static DrawingPoint ReadPoint(JsonElement point)
    {
        var coordinates = point.EnumerateArray()
            .Select(coordinate => coordinate.GetDouble())
            .ToArray();

        if (coordinates.Length != 2)
        {
            throw new InvalidOperationException("DrawingSpec point arrays must contain exactly two coordinates.");
        }

        return new DrawingPoint(coordinates[0], coordinates[1]);
    }

    private static string ReadString(JsonElement obj, string propertyName)
    {
        return obj.TryGetProperty(propertyName, out var value) ? value.GetString() ?? string.Empty : string.Empty;
    }

    private static int ReadInt(JsonElement obj, string propertyName)
    {
        return obj.TryGetProperty(propertyName, out var value) ? value.GetInt32() : 0;
    }

    private static double ReadDouble(JsonElement obj, string propertyName)
    {
        return obj.TryGetProperty(propertyName, out var value) ? value.GetDouble() : 0d;
    }

    private static bool ReadBoolean(JsonElement obj, string propertyName)
    {
        return obj.TryGetProperty(propertyName, out var value) && value.GetBoolean();
    }

    private static string ReadPluginSource()
    {
        var root = FindRepositoryRoot();
        var pluginDirectory = Path.Combine(root, "src", "ZwcadAiPlugin");

        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(pluginDirectory, "*.cs")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static IReadOnlyList<string> ReadProjectReferences(string projectPath)
    {
        var document = XDocument.Load(projectPath);

        return document
            .Descendants("ProjectReference")
            .Select(reference => (string?)reference.Attribute("Include") ?? string.Empty)
            .Select(path => Path.GetFileName(path) ?? string.Empty)
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .ToArray();
    }

    private static bool IsZwcadRuntimeReference(string reference)
    {
        return reference.StartsWith("Zw", StringComparison.OrdinalIgnoreCase)
            || reference.StartsWith("Zcad", StringComparison.OrdinalIgnoreCase)
            || reference.IndexOf("ZWCAD", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void AssertZwcadReference(XElement reference, string assemblyFileName)
    {
        var hintPath = reference.Element("HintPath")?.Value ?? string.Empty;
        var copyLocal = reference.Element("Private")?.Value ?? string.Empty;

        Assert(
            hintPath.EndsWith(assemblyFileName, StringComparison.OrdinalIgnoreCase),
            $"{assemblyFileName} reference must use a HintPath ending in {assemblyFileName}.");
        Assert(
            hintPath.IndexOf(@"C:\Program Files", StringComparison.OrdinalIgnoreCase) < 0,
            $"{assemblyFileName} reference must not hardcode a local absolute install path.");
        AssertEqual("false", copyLocal.ToLowerInvariant());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ZwcadAi.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }

    private static void AssertPoint(double expectedX, double expectedY, DrawingPoint? actual)
    {
        if (actual == null)
        {
            throw new InvalidOperationException($"Expected point ({expectedX}, {expectedY}) but got null.");
        }

        AssertEqual(expectedX, actual.X);
        AssertEqual(expectedY, actual.Y);
    }

    private static bool PointsEqual(DrawingPoint? expected, DrawingPoint? actual)
    {
        return expected != null
            && actual != null
            && Math.Abs(expected.X - actual.X) < 0.000001
            && Math.Abs(expected.Y - actual.Y) < 0.000001;
    }

    private static double CenterLineLength(PlannedEntity entity)
    {
        if (entity.Start == null || entity.End == null)
        {
            throw new InvalidOperationException($"Centerline '{entity.SpecEntityId}' must include start and end points.");
        }

        var dx = entity.End.X - entity.Start.X;
        var dy = entity.End.Y - entity.Start.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static string MinimalSpecJson(string entityJson)
    {
        return $$"""
        {
          "drawingSpecVersion": "1.0",
          "units": "mm",
          "metadata": {
            "title": "schema validation test",
            "domain": "mechanical_plate",
            "createdBy": "test",
            "requestId": "schema-validation-test"
          },
          "layers": [
            { "name": "OUTLINE", "color": 7, "lineType": "Continuous", "lineWeight": 0.35 },
            { "name": "CENTER", "color": 1, "lineType": "Center", "lineWeight": 0.18 },
            { "name": "DIM", "color": 3, "lineType": "Continuous", "lineWeight": 0.18 }
          ],
          "entities": [
            {{entityJson}}
          ],
          "dimensions": [],
          "clarifications": []
        }
        """;
    }

    private static string FormatIssues(IEnumerable<ValidationIssue> issues)
    {
        return string.Join(
            "; ",
            issues.Select(issue => $"{issue.Code} at {issue.Path}: {issue.Message}"));
    }

    private static void AssertSequenceEqual<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(
                $"Expected sequence [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}].");
        }
    }
}
