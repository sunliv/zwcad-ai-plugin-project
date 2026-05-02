using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ZwcadAi.Core;

public sealed class CadIntentCompilationResult
{
    public CadIntentCompilationResult(
        CadJsonInputKind inputKind,
        DrawingSpec? drawingSpec,
        ValidationResult validation,
        IReadOnlyList<string> clarifications)
    {
        InputKind = inputKind;
        DrawingSpec = drawingSpec;
        Validation = validation ?? throw new ArgumentNullException(nameof(validation));
        Clarifications = clarifications ?? Array.Empty<string>();
    }

    public CadJsonInputKind InputKind { get; }

    public DrawingSpec? DrawingSpec { get; }

    public ValidationResult Validation { get; }

    public IReadOnlyList<string> Clarifications { get; }

    public bool Success => DrawingSpec != null && Validation.IsValid;
}

public static class CadIntentCompiler
{
    private const double OverallDimensionOffset = 12d;
    private const double FeatureDimensionOffset = 18d;
    private const double DefaultCenterMarkSize = 10d;

    public static CadIntentCompilationResult Compile(string json)
    {
        var classification = CadJsonInputClassifier.Classify(json);
        if (!classification.Validation.IsValid)
        {
            return new CadIntentCompilationResult(
                classification.Kind,
                null,
                classification.Validation,
                classification.Clarifications);
        }

        if (classification.Kind != CadJsonInputKind.TemplateIntent
            && classification.Kind != CadJsonInputKind.CompositeIntent)
        {
            return Failure(
                classification.Kind,
                CadIntentIssueCodes.UnsupportedJsonContract,
                "$.intentType",
                $"CadIntent compiler supports {CadIntentTypes.TemplateIntent} and {CadIntentTypes.CompositeIntent} for {CadIntentDomainPacks.MechanicalPlate}.");
        }

        CadIntentJsonValue root;
        try
        {
            root = CadIntentJsonParser.Parse(json);
        }
        catch (CadIntentJsonParseException exception)
        {
            return Failure(
                classification.Kind,
                CadIntentIssueCodes.UnsupportedJsonContract,
                "$",
                exception.Message);
        }

        var engineeringValidation = classification.Kind == CadJsonInputKind.CompositeIntent
            ? ValidateCompositeEngineeringRules(root)
            : ValidationResult.Success();
        if (!engineeringValidation.IsValid)
        {
            return new CadIntentCompilationResult(
                classification.Kind,
                null,
                engineeringValidation,
                Array.Empty<string>());
        }

        var spec = classification.Kind == CadJsonInputKind.TemplateIntent
            ? CompileTemplateIntent(root)
            : CompileCompositeIntent(root);
        var validation = ValidateCompiledSpec(spec);

        return new CadIntentCompilationResult(
            classification.Kind,
            spec,
            validation,
            Array.Empty<string>());
    }

    private static DrawingSpec CompileTemplateIntent(CadIntentJsonValue root)
    {
        var parameters = ReadObject(root, "parameters")!;
        var length = ReadNumber(parameters, "length");
        var width = ReadNumber(parameters, "width");

        return CreateMechanicalPlateSpec(
            "rectangular plate",
            "cadintent-rectangular-plate",
            length,
            width,
            Array.Empty<CadIntentJsonValue>());
    }

    private static DrawingSpec CompileCompositeIntent(CadIntentJsonValue root)
    {
        var baseProfile = ReadObject(root, "baseProfile")!;
        var size = ReadObject(baseProfile, "size")!;
        var length = ReadNumber(size, "length");
        var width = ReadNumber(size, "width");
        var features = ReadArray(root, "features") ?? Array.Empty<CadIntentJsonValue>();

        return CreateMechanicalPlateSpec(
            "mechanical plate",
            "cadintent-mechanical-plate",
            length,
            width,
            features);
    }

    private static DrawingSpec CreateMechanicalPlateSpec(
        string titlePrefix,
        string requestIdPrefix,
        double length,
        double width,
        IReadOnlyList<CadIntentJsonValue> features)
    {
        var filletRadius = ReadFirstFilletRadius(features);
        var entities = new List<EntitySpec>();
        if (filletRadius.HasValue)
        {
            entities.AddRange(CreateRoundedOuterProfile(length, width, filletRadius.Value));
        }
        else
        {
            entities.Add(CreateOuterProfile(length, width));
        }

        var dimensions = new List<DimensionSpec>
        {
            CreateLinearDimension(
                "dim-overall-length",
                new DrawingPoint(0, 0),
                new DrawingPoint(length, 0),
                new DrawingPoint(0, -OverallDimensionOffset),
                FormatNumber(length)),
            CreateLinearDimension(
                "dim-overall-width",
                new DrawingPoint(length, 0),
                new DrawingPoint(length, width),
                new DrawingPoint(OverallDimensionOffset, 0),
                FormatNumber(width))
        };

        AddFeatures(features, entities, dimensions);

        return new DrawingSpec
        {
            DrawingSpecVersion = DrawingSpecWireFormat.Version,
            Units = "mm",
            Metadata = new DrawingMetadata
            {
                Title = $"{FormatNumber(length)}x{FormatNumber(width)} {titlePrefix}",
                Domain = DrawingDomain.MechanicalPlate,
                CreatedBy = "cadintent-local-compiler",
                RequestId = $"{requestIdPrefix}-{FormatNumberForId(length)}x{FormatNumberForId(width)}"
            },
            Layers = StandardProductionLayers(),
            Entities = entities.ToArray(),
            Dimensions = dimensions.ToArray(),
            Clarifications = Array.Empty<string>()
        };
    }

    private static EntitySpec CreateOuterProfile(double length, double width)
    {
        return new EntitySpec
        {
            Id = "outer-profile",
            Type = EntityTypes.Polyline,
            Layer = CadLayerNames.Outline,
            Closed = true,
            Points = new[]
            {
                new DrawingPoint(0, 0),
                new DrawingPoint(length, 0),
                new DrawingPoint(length, width),
                new DrawingPoint(0, width)
            }
        };
    }

    private static IReadOnlyList<EntitySpec> CreateRoundedOuterProfile(
        double length,
        double width,
        double radius)
    {
        return new[]
        {
            CreateLine(
                "outer-profile-line-bottom",
                new DrawingPoint(radius, 0),
                new DrawingPoint(length - radius, 0)),
            CreateLine(
                "outer-profile-line-right",
                new DrawingPoint(length, radius),
                new DrawingPoint(length, width - radius)),
            CreateLine(
                "outer-profile-line-top",
                new DrawingPoint(length - radius, width),
                new DrawingPoint(radius, width)),
            CreateLine(
                "outer-profile-line-left",
                new DrawingPoint(0, width - radius),
                new DrawingPoint(0, radius)),
            CreateArc(
                "fillet-1-bottom-left",
                new DrawingPoint(radius, radius),
                radius,
                180d,
                270d),
            CreateArc(
                "fillet-1-bottom-right",
                new DrawingPoint(length - radius, radius),
                radius,
                270d,
                360d),
            CreateArc(
                "fillet-1-top-right",
                new DrawingPoint(length - radius, width - radius),
                radius,
                0d,
                90d),
            CreateArc(
                "fillet-1-top-left",
                new DrawingPoint(radius, width - radius),
                radius,
                90d,
                180d)
        };
    }

    private static void AddFeatures(
        IReadOnlyList<CadIntentJsonValue> features,
        ICollection<EntitySpec> entities,
        ICollection<DimensionSpec> dimensions)
    {
        var holeCount = 0;
        var slotCount = 0;
        var filletCount = 0;
        var centerMarkCount = 0;

        foreach (var feature in features)
        {
            if (feature.Kind != CadIntentJsonValueKind.Object)
            {
                continue;
            }

            var type = ReadString(feature, "type");
            switch (type)
            {
                case "hole":
                    holeCount++;
                    AddHoleFeature(feature, holeCount, entities, dimensions);
                    break;
                case "slot":
                    slotCount++;
                    AddSlotFeature(feature, slotCount, entities, dimensions);
                    break;
                case "fillet":
                    filletCount++;
                    AddFilletFeature(feature, filletCount, dimensions);
                    break;
                case "centerMark":
                case "center_mark":
                    centerMarkCount++;
                    AddCenterMarkFeature(feature, centerMarkCount, entities);
                    break;
            }
        }
    }

    private static void AddHoleFeature(
        CadIntentJsonValue feature,
        int index,
        ICollection<EntitySpec> entities,
        ICollection<DimensionSpec> dimensions)
    {
        var id = $"hole-{index}";
        var center = ReadPoint(feature, "center");
        var diameter = ReadNumber(feature, "diameter");

        entities.Add(new EntitySpec
        {
            Id = id,
            Type = EntityTypes.Circle,
            Layer = CadLayerNames.Outline,
            Center = center,
            Radius = diameter / 2d
        });
        entities.Add(CreateCenterMark($"{id}-center", center, CenterMarkSize(diameter)));
        dimensions.Add(new DimensionSpec
        {
            Id = $"dim-{id}-diameter",
            Type = DimensionTypes.Diameter,
            Layer = CadLayerNames.Dimension,
            TargetEntityId = id,
            Text = $"%%c{FormatNumber(diameter)}"
        });
    }

    private static void AddSlotFeature(
        CadIntentJsonValue feature,
        int index,
        ICollection<EntitySpec> entities,
        ICollection<DimensionSpec> dimensions)
    {
        var id = $"slot-{index}";
        var center = ReadPoint(feature, "center");
        var length = ReadNumber(feature, "length");
        var width = ReadNumber(feature, "width");
        var angle = ReadNumber(feature, "angle");
        var radius = width / 2d;
        var halfLength = length / 2d;
        var straightHalfLength = Math.Max(0d, (length - width) / 2d);
        var direction = UnitVector(angle);
        var normal = new DrawingPoint(-direction.Y, direction.X);
        var startCenter = Offset(center, direction, -straightHalfLength);
        var endCenter = Offset(center, direction, straightHalfLength);

        entities.Add(CreateLine(
            $"{id}-line-top",
            Offset(startCenter, normal, radius),
            Offset(endCenter, normal, radius)));
        entities.Add(CreateLine(
            $"{id}-line-bottom",
            Offset(endCenter, normal, -radius),
            Offset(startCenter, normal, -radius)));
        entities.Add(CreateArc(
            $"{id}-arc-start",
            startCenter,
            radius,
            angle + 90d,
            angle + 270d));
        entities.Add(CreateArc(
            $"{id}-arc-end",
            endCenter,
            radius,
            angle - 90d,
            angle + 90d));
        entities.Add(CreateCenterMark($"{id}-center", center, CenterMarkSize(width)));

        dimensions.Add(CreateLinearDimension(
            $"dim-{id}-length",
            Offset(center, direction, -halfLength),
            Offset(center, direction, halfLength),
            Offset(new DrawingPoint(0, 0), normal, -FeatureDimensionOffset),
            FormatNumber(length)));
        dimensions.Add(CreateLinearDimension(
            $"dim-{id}-width",
            Offset(center, normal, -radius),
            Offset(center, normal, radius),
            Offset(new DrawingPoint(0, 0), direction, FeatureDimensionOffset),
            FormatNumber(width)));
    }

    private static void AddFilletFeature(
        CadIntentJsonValue feature,
        int index,
        ICollection<DimensionSpec> dimensions)
    {
        var id = $"fillet-{index}";
        var radius = ReadNumber(feature, "radius");

        dimensions.Add(new DimensionSpec
        {
            Id = $"dim-{id}-radius",
            Type = DimensionTypes.Radius,
            Layer = CadLayerNames.Dimension,
            TargetEntityId = $"{id}-top-right",
            Text = $"R{FormatNumber(radius)}"
        });
    }

    private static void AddCenterMarkFeature(
        CadIntentJsonValue feature,
        int index,
        ICollection<EntitySpec> entities)
    {
        entities.Add(CreateCenterMark($"center-mark-{index}", ReadPoint(feature, "center"), DefaultCenterMarkSize));
    }

    private static EntitySpec CreateLine(string id, DrawingPoint start, DrawingPoint end)
    {
        return new EntitySpec
        {
            Id = id,
            Type = EntityTypes.Line,
            Layer = CadLayerNames.Outline,
            Start = start,
            End = end
        };
    }

    private static EntitySpec CreateArc(
        string id,
        DrawingPoint center,
        double radius,
        double startAngle,
        double endAngle)
    {
        return new EntitySpec
        {
            Id = id,
            Type = EntityTypes.Arc,
            Layer = CadLayerNames.Outline,
            Center = center,
            Radius = radius,
            StartAngle = NormalizeAngle(startAngle),
            EndAngle = NormalizeAngle(endAngle)
        };
    }

    private static EntitySpec CreateCenterMark(string id, DrawingPoint center, double size)
    {
        return new EntitySpec
        {
            Id = id,
            Type = EntityTypes.CenterMark,
            Layer = CadLayerNames.Center,
            Center = center,
            Size = size
        };
    }

    private static DimensionSpec CreateLinearDimension(
        string id,
        DrawingPoint from,
        DrawingPoint to,
        DrawingPoint offset,
        string text)
    {
        return new DimensionSpec
        {
            Id = id,
            Type = DimensionTypes.Linear,
            Layer = CadLayerNames.Dimension,
            From = from,
            To = to,
            Offset = offset,
            Text = text
        };
    }

    private static IReadOnlyList<LayerSpec> StandardProductionLayers()
    {
        return CadLayerStandards.RequiredProductionLayerNames
            .Select(name => CadLayerStandards.Definitions[name].ToLayerSpec())
            .ToArray();
    }

    private static ValidationResult ValidateCompositeEngineeringRules(CadIntentJsonValue root)
    {
        var issues = new List<ValidationIssue>();
        var baseProfile = ReadObject(root, "baseProfile")!;
        var size = ReadObject(baseProfile, "size")!;
        var plateLength = ReadNumber(size, "length");
        var plateWidth = ReadNumber(size, "width");
        var maxFilletRadius = Math.Min(plateLength, plateWidth) / 2d;
        var features = ReadArray(root, "features") ?? Array.Empty<CadIntentJsonValue>();
        var filletCount = 0;

        for (var index = 0; index < features.Count; index++)
        {
            var feature = features[index];
            if (feature.Kind != CadIntentJsonValueKind.Object)
            {
                continue;
            }

            var path = $"$.features[{index}]";
            var type = ReadString(feature, "type");
            switch (type)
            {
                case "slot":
                    var length = ReadNumber(feature, "length");
                    var width = ReadNumber(feature, "width");
                    if (length <= width)
                    {
                        AddIssue(
                            issues,
                            CadIntentIssueCodes.MissingRequiredParameter,
                            $"{path}.length",
                            "Slot length must be greater than slot width.");
                    }

                    break;
                case "fillet":
                    filletCount++;
                    var radius = ReadNumber(feature, "radius");
                    if (radius >= maxFilletRadius)
                    {
                        AddIssue(
                            issues,
                            CadIntentIssueCodes.MissingRequiredParameter,
                            $"{path}.radius",
                            "Fillet radius must be less than half of the shortest base profile side.");
                    }

                    if (filletCount > 1)
                    {
                        AddIssue(
                            issues,
                            CadIntentIssueCodes.MissingRequiredParameter,
                            $"{path}.type",
                            "CompositeIntent mechanical_plate supports one all-corner fillet feature.");
                    }

                    break;
            }
        }

        return issues.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(issues);
    }

    private static ValidationResult ValidateCompiledSpec(DrawingSpec spec)
    {
        var schema = DrawingSpecValidator.ValidateSchema(spec);
        if (!schema.IsValid)
        {
            return schema;
        }

        return DrawingSpecValidator.ValidateBusinessRules(spec);
    }

    private static double? ReadFirstFilletRadius(IReadOnlyList<CadIntentJsonValue> features)
    {
        foreach (var feature in features)
        {
            if (feature.Kind == CadIntentJsonValueKind.Object
                && string.Equals(ReadString(feature, "type"), "fillet", StringComparison.Ordinal))
            {
                return ReadNumber(feature, "radius");
            }
        }

        return null;
    }

    private static CadIntentCompilationResult Failure(
        CadJsonInputKind kind,
        string code,
        string path,
        string message)
    {
        return new CadIntentCompilationResult(
            kind,
            null,
            ValidationResult.Failure(new[]
            {
                new ValidationIssue(code, path, message, ValidationSeverity.Error)
            }),
            Array.Empty<string>());
    }

    private static void AddIssue(ICollection<ValidationIssue> issues, string code, string path, string message)
    {
        issues.Add(new ValidationIssue(code, path, message, ValidationSeverity.Error));
    }

    private static CadIntentJsonValue? GetProperty(CadIntentJsonValue value, string propertyName)
    {
        return value.ObjectProperties.TryGetValue(propertyName, out var property) ? property : null;
    }

    private static CadIntentJsonValue? ReadObject(CadIntentJsonValue value, string propertyName)
    {
        var property = GetProperty(value, propertyName);
        return property != null && property.Kind == CadIntentJsonValueKind.Object ? property : null;
    }

    private static IReadOnlyList<CadIntentJsonValue>? ReadArray(CadIntentJsonValue value, string propertyName)
    {
        var property = GetProperty(value, propertyName);
        return property != null && property.Kind == CadIntentJsonValueKind.Array ? property.ArrayItems : null;
    }

    private static string ReadString(CadIntentJsonValue value, string propertyName)
    {
        var property = GetProperty(value, propertyName);
        return property != null && property.Kind == CadIntentJsonValueKind.String ? property.StringValue : string.Empty;
    }

    private static double ReadNumber(CadIntentJsonValue value, string propertyName)
    {
        var property = GetProperty(value, propertyName);
        return property != null && property.Kind == CadIntentJsonValueKind.Number ? property.NumberValue : 0d;
    }

    private static DrawingPoint ReadPoint(CadIntentJsonValue value, string propertyName)
    {
        var property = GetProperty(value, propertyName);
        if (property == null || property.Kind != CadIntentJsonValueKind.Array || property.ArrayItems.Count != 2)
        {
            return new DrawingPoint();
        }

        return new DrawingPoint(property.ArrayItems[0].NumberValue, property.ArrayItems[1].NumberValue);
    }

    private static DrawingPoint UnitVector(double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180d;
        return new DrawingPoint(Math.Cos(radians), Math.Sin(radians));
    }

    private static DrawingPoint Offset(DrawingPoint point, DrawingPoint vector, double distance)
    {
        return new DrawingPoint(
            RoundCoordinate(point.X + (vector.X * distance)),
            RoundCoordinate(point.Y + (vector.Y * distance)));
    }

    private static double NormalizeAngle(double angle)
    {
        var normalized = RoundCoordinate(angle);
        if (normalized < 0)
        {
            normalized += 360d;
        }

        if (normalized > 360d)
        {
            normalized %= 360d;
            if (Math.Abs(normalized) < 0.000001)
            {
                return 360d;
            }
        }

        return normalized;
    }

    private static double CenterMarkSize(double featureSize)
    {
        return Math.Max(DefaultCenterMarkSize, featureSize / 2d);
    }

    private static double RoundCoordinate(double value)
    {
        return Math.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private static string FormatNumber(double value)
    {
        return RoundCoordinate(value).ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string FormatNumberForId(double value)
    {
        return FormatNumber(value).Replace(".", "p");
    }
}
