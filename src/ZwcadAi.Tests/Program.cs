using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            ("Renderer rejects specs missing production layers", RendererRejectsSpecsMissingProductionLayers),
            ("Renderer result preserves spec-to-CAD mapping", RendererResultPreservesMapping),
            ("Project references follow architecture boundaries", ProjectReferencesFollowArchitectureBoundaries),
            ("Core project has no ZWCAD runtime references", CoreProjectHasNoZwcadRuntimeReferences),
            ("Plugin references ZWCAD 2025 managed assemblies", PluginReferencesZwcad2025ManagedAssemblies),
            ("Plugin registers AIDRAW command", PluginRegistersAiDrawCommand),
            ("Plugin AIDRAW uses fixed POC sample and transaction writer", PluginAiDrawUsesFixedPocSampleAndTransactionWriter),
            ("Plugin registers AIEXPORT command", PluginRegistersAiExportCommand),
            ("AIEXPORT saves a DWG copy without saving the active drawing", PluginAiExportSavesDwgCopyWithoutSavingActiveDrawing),
            ("AIEXPORT covers the PDF plot-to-file path", PluginAiExportCoversPdfPlotToFilePath)
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
