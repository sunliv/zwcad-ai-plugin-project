using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ZwcadAi.Core;

public sealed class CadIntentSpec
{
    public string CadIntentSpecVersion { get; set; } = CadIntentSpecWireFormat.Version;

    public string IntentType { get; set; } = string.Empty;

    public string DomainPack { get; set; } = string.Empty;

    public string Units { get; set; } = "mm";
}

public static class CadIntentSpecWireFormat
{
    public const string Version = "1.0";
}

public static class CadIntentTypes
{
    public const string TemplateIntent = "TemplateIntent";
    public const string CompositeIntent = "CompositeIntent";
    public const string SketchIntent = "SketchIntent";
}

public static class CadIntentDomainPacks
{
    public const string MechanicalPlate = "mechanical_plate";
    public const string Generic2dMechanical = "generic_2d_mechanical";
}

public static class CadIntentTemplates
{
    public const string RectangularPlate = "rectangular_plate";
}

public static class CadIntentIssueCodes
{
    public const string MissingRequiredParameter = "missing_required_parameter";
    public const string UnsupportedDomainPack = "unsupported_domain_pack";
    public const string UnsupportedTemplate = "unsupported_template";
    public const string UnsupportedFeatureType = "unsupported_feature_type";
    public const string UnsupportedSegmentType = "unsupported_segment_type";
    public const string ProfileNotClosed = "profile_not_closed";
    public const string UnsupportedJsonContract = "unsupported_json_contract";
}

public enum CadJsonInputKind
{
    Unsupported = 0,
    TemplateIntent = 1,
    CompositeIntent = 2,
    SketchIntent = 3,
    DrawingSpec = 4
}

public sealed class CadJsonInputClassification
{
    public CadJsonInputClassification(
        CadJsonInputKind kind,
        ValidationResult validation,
        IReadOnlyList<string> clarifications)
    {
        Kind = kind;
        Validation = validation ?? throw new ArgumentNullException(nameof(validation));
        Clarifications = clarifications ?? Array.Empty<string>();
    }

    public CadJsonInputKind Kind { get; }

    public ValidationResult Validation { get; }

    public IReadOnlyList<string> Clarifications { get; }
}

public static class CadJsonInputClassifier
{
    private const double PointTolerance = 0.000001;

    private static readonly HashSet<string> CompositeFeatureTypes = new HashSet<string>(
        new[] { "hole", "slot", "fillet", "centerMark", "center_mark" },
        StringComparer.Ordinal);

    private static readonly HashSet<string> SketchFeatureTypes = new HashSet<string>(
        new[] { "hole", "slot", "text", "mtext" },
        StringComparer.Ordinal);

    private static readonly HashSet<string> SketchSegmentTypes = new HashSet<string>(
        new[] { "line", "arc" },
        StringComparer.Ordinal);

    public static CadJsonInputClassification Classify(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Unsupported("$", "CadIntent or DrawingSpec JSON is required.");
        }

        CadIntentJsonValue root;
        try
        {
            root = CadIntentJsonParser.Parse(json);
        }
        catch (CadIntentJsonParseException exception)
        {
            return Unsupported("$", exception.Message);
        }

        if (root.Kind != CadIntentJsonValueKind.Object)
        {
            return Unsupported("$", "CadIntent or DrawingSpec root must be a JSON object.");
        }

        if (HasProperty(root, "drawingSpecVersion"))
        {
            return new CadJsonInputClassification(
                CadJsonInputKind.DrawingSpec,
                DrawingSpecValidator.ValidateSchemaJson(json),
                Array.Empty<string>());
        }

        if (!HasProperty(root, "cadIntentSpecVersion") || !HasProperty(root, "intentType"))
        {
            return Unsupported("$", "JSON root is neither DrawingSpec v1 nor CadIntentSpec v1.");
        }

        var issues = new List<ValidationIssue>();
        var clarifications = new List<string>();
        ValidateCadIntentVersion(root, issues);

        var intentType = ReadString(root, "intentType");
        switch (intentType)
        {
            case CadIntentTypes.TemplateIntent:
                ValidateTemplateIntent(root, issues, clarifications);
                return Create(CadJsonInputKind.TemplateIntent, issues, clarifications);
            case CadIntentTypes.CompositeIntent:
                ValidateCompositeIntent(root, issues, clarifications);
                return Create(CadJsonInputKind.CompositeIntent, issues, clarifications);
            case CadIntentTypes.SketchIntent:
                ValidateSketchIntent(root, issues, clarifications);
                return Create(CadJsonInputKind.SketchIntent, issues, clarifications);
            default:
                AddIssue(
                    issues,
                    CadIntentIssueCodes.UnsupportedJsonContract,
                    "$.intentType",
                    $"Intent type '{intentType}' is not part of CadIntentSpec v1.");
                return Create(CadJsonInputKind.Unsupported, issues, clarifications);
        }
    }

    private static void ValidateCadIntentVersion(CadIntentJsonValue root, ICollection<ValidationIssue> issues)
    {
        var version = ReadString(root, "cadIntentSpecVersion");
        if (!string.Equals(version, CadIntentSpecWireFormat.Version, StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                CadIntentIssueCodes.UnsupportedJsonContract,
                "$.cadIntentSpecVersion",
                $"CadIntentSpec version must be '{CadIntentSpecWireFormat.Version}'.");
        }

        var units = ReadString(root, "units");
        if (string.IsNullOrWhiteSpace(units))
        {
            AddIssue(
                issues,
                CadIntentIssueCodes.MissingRequiredParameter,
                "$.units",
                "Units are required; CadIntentSpec v1 MVP accepts 'mm'.");
        }
        else if (!string.Equals(units, "mm", StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                CadIntentIssueCodes.UnsupportedJsonContract,
                "$.units",
                "CadIntentSpec v1 MVP accepts only millimeters.");
        }
    }

    private static void ValidateTemplateIntent(
        CadIntentJsonValue root,
        ICollection<ValidationIssue> issues,
        ICollection<string> clarifications)
    {
        ValidateDomainPack(root, CadIntentDomainPacks.MechanicalPlate, issues);

        var template = ReadString(root, "template");
        if (string.IsNullOrWhiteSpace(template))
        {
            AddMissing(issues, clarifications, "$.template", "TemplateIntent requires a template name.");
        }
        else if (!string.Equals(template, CadIntentTemplates.RectangularPlate, StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                CadIntentIssueCodes.UnsupportedTemplate,
                "$.template",
                $"Template '{template}' is not supported by CadIntentSpec v1.");
        }

        var parameters = ReadObject(root, "parameters");
        if (parameters == null)
        {
            AddMissing(issues, clarifications, "$.parameters", "TemplateIntent requires a parameters object.");
            return;
        }

        ValidatePositiveNumber(parameters, "length", "$.parameters.length", issues, clarifications);
        ValidatePositiveNumber(parameters, "width", "$.parameters.width", issues, clarifications);
    }

    private static void ValidateCompositeIntent(
        CadIntentJsonValue root,
        ICollection<ValidationIssue> issues,
        ICollection<string> clarifications)
    {
        ValidateDomainPack(root, CadIntentDomainPacks.MechanicalPlate, issues);

        var baseProfile = ReadObject(root, "baseProfile");
        if (baseProfile == null)
        {
            AddMissing(issues, clarifications, "$.baseProfile", "CompositeIntent requires a baseProfile object.");
        }
        else
        {
            var baseProfileType = ReadString(baseProfile, "type");
            if (!string.Equals(baseProfileType, "rectangle", StringComparison.Ordinal))
            {
                AddIssue(
                    issues,
                    CadIntentIssueCodes.UnsupportedTemplate,
                    "$.baseProfile.type",
                    $"Base profile type '{baseProfileType}' is not supported by CadIntentSpec v1.");
            }

            var size = ReadObject(baseProfile, "size");
            if (size == null)
            {
                AddMissing(issues, clarifications, "$.baseProfile.size", "Rectangle baseProfile requires size.");
            }
            else
            {
                ValidatePositiveNumber(size, "length", "$.baseProfile.size.length", issues, clarifications);
                ValidatePositiveNumber(size, "width", "$.baseProfile.size.width", issues, clarifications);
            }
        }

        ValidateFeatures(root, "features", "$.features", CompositeFeatureTypes, issues, clarifications);
    }

    private static void ValidateSketchIntent(
        CadIntentJsonValue root,
        ICollection<ValidationIssue> issues,
        ICollection<string> clarifications)
    {
        ValidateDomainPack(root, CadIntentDomainPacks.Generic2dMechanical, issues);

        var profile = ReadObject(root, "profile");
        if (profile == null)
        {
            AddMissing(issues, clarifications, "$.profile", "SketchIntent requires a profile object.");
            return;
        }

        if (!ReadBoolean(profile, "closed"))
        {
            AddProfileNotClosed(issues, clarifications, "SketchIntent profile must explicitly set closed to true.");
        }

        var segments = ReadArray(profile, "segments");
        if (segments == null || segments.Count == 0)
        {
            AddMissing(issues, clarifications, "$.profile.segments", "SketchIntent profile requires one or more segments.");
        }
        else
        {
            ValidateSketchSegments(segments, issues, clarifications);
        }

        ValidateFeatures(root, "features", "$.features", SketchFeatureTypes, issues, clarifications);
    }

    private static void ValidateSketchSegments(
        IReadOnlyList<CadIntentJsonValue> segments,
        ICollection<ValidationIssue> issues,
        ICollection<string> clarifications)
    {
        DrawingPoint? firstStart = null;
        DrawingPoint? previousEnd = null;
        var canCheckClosure = true;

        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            var path = $"$.profile.segments[{index}]";
            if (segment.Kind != CadIntentJsonValueKind.Object)
            {
                AddIssue(issues, CadIntentIssueCodes.UnsupportedJsonContract, path, "Profile segment must be an object.");
                canCheckClosure = false;
                continue;
            }

            var type = ReadString(segment, "type");
            if (!SketchSegmentTypes.Contains(type))
            {
                AddIssue(
                    issues,
                    CadIntentIssueCodes.UnsupportedSegmentType,
                    $"{path}.type",
                    $"Segment type '{type}' is not supported by CadIntentSpec v1.");
                canCheckClosure = false;
                continue;
            }

            var start = ValidatePoint(segment, "start", $"{path}.start", issues, clarifications);
            var end = ValidatePoint(segment, "end", $"{path}.end", issues, clarifications);

            if (string.Equals(type, "arc", StringComparison.Ordinal))
            {
                ValidatePoint(segment, "center", $"{path}.center", issues, clarifications);
                ValidatePositiveNumber(segment, "radius", $"{path}.radius", issues, clarifications);
                ValidateNumber(segment, "startAngle", $"{path}.startAngle", issues, clarifications);
                ValidateNumber(segment, "endAngle", $"{path}.endAngle", issues, clarifications);
            }

            if (start == null || end == null)
            {
                canCheckClosure = false;
                continue;
            }

            if (firstStart == null)
            {
                firstStart = start;
            }

            if (previousEnd != null && !PointsEqual(previousEnd, start))
            {
                AddProfileNotClosed(issues, clarifications, $"Segment {index} does not start where the previous segment ends.");
                canCheckClosure = false;
            }

            previousEnd = end;
        }

        if (canCheckClosure && firstStart != null && previousEnd != null && !PointsEqual(previousEnd, firstStart))
        {
            AddProfileNotClosed(issues, clarifications, "SketchIntent profile endpoint does not return to the start point.");
        }
    }

    private static void ValidateFeatures(
        CadIntentJsonValue root,
        string propertyName,
        string path,
        HashSet<string> allowedTypes,
        ICollection<ValidationIssue> issues,
        ICollection<string> clarifications)
    {
        var features = ReadArray(root, propertyName);
        if (features == null)
        {
            return;
        }

        for (var index = 0; index < features.Count; index++)
        {
            var feature = features[index];
            var featurePath = $"{path}[{index}]";
            if (feature.Kind != CadIntentJsonValueKind.Object)
            {
                AddIssue(issues, CadIntentIssueCodes.UnsupportedJsonContract, featurePath, "Feature must be an object.");
                continue;
            }

            var type = ReadString(feature, "type");
            if (!allowedTypes.Contains(type))
            {
                AddIssue(
                    issues,
                    CadIntentIssueCodes.UnsupportedFeatureType,
                    $"{featurePath}.type",
                    $"Feature type '{type}' is not supported by CadIntentSpec v1.");
                continue;
            }

            ValidateFeatureParameters(feature, type, featurePath, issues, clarifications);
        }
    }

    private static void ValidateFeatureParameters(
        CadIntentJsonValue feature,
        string featureType,
        string path,
        ICollection<ValidationIssue> issues,
        ICollection<string> clarifications)
    {
        switch (featureType)
        {
            case "hole":
                ValidatePoint(feature, "center", $"{path}.center", issues, clarifications);
                ValidatePositiveNumber(feature, "diameter", $"{path}.diameter", issues, clarifications);
                break;
            case "slot":
                ValidatePoint(feature, "center", $"{path}.center", issues, clarifications);
                ValidatePositiveNumber(feature, "length", $"{path}.length", issues, clarifications);
                ValidatePositiveNumber(feature, "width", $"{path}.width", issues, clarifications);
                ValidateNumber(feature, "angle", $"{path}.angle", issues, clarifications);
                break;
            case "fillet":
                ValidatePositiveNumber(feature, "radius", $"{path}.radius", issues, clarifications);
                break;
            case "centerMark":
            case "center_mark":
                ValidatePoint(feature, "center", $"{path}.center", issues, clarifications);
                break;
            case "text":
            case "mtext":
                ValidatePoint(feature, "position", $"{path}.position", issues, clarifications);
                ValidateRequiredString(feature, "value", $"{path}.value", issues, clarifications);
                break;
        }
    }

    private static void ValidateDomainPack(
        CadIntentJsonValue root,
        string expectedDomainPack,
        ICollection<ValidationIssue> issues)
    {
        var domainPack = ReadString(root, "domainPack");
        if (string.IsNullOrWhiteSpace(domainPack))
        {
            AddIssue(
                issues,
                CadIntentIssueCodes.MissingRequiredParameter,
                "$.domainPack",
                $"Domain pack '{expectedDomainPack}' is required.");
            return;
        }

        if (!string.Equals(domainPack, expectedDomainPack, StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                CadIntentIssueCodes.UnsupportedDomainPack,
                "$.domainPack",
                $"Domain pack '{domainPack}' is not supported for this intent type.");
        }
    }

    private static void ValidateRequiredString(
        CadIntentJsonValue obj,
        string propertyName,
        string path,
        ICollection<ValidationIssue> issues,
        ICollection<string> clarifications)
    {
        var value = ReadString(obj, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            AddMissing(issues, clarifications, path, $"Parameter '{propertyName}' is required.");
        }
    }

    private static void ValidatePositiveNumber(
        CadIntentJsonValue obj,
        string propertyName,
        string path,
        ICollection<ValidationIssue> issues,
        ICollection<string> clarifications)
    {
        var value = GetProperty(obj, propertyName);
        if (value == null || value.Kind != CadIntentJsonValueKind.Number || value.NumberValue <= 0)
        {
            AddMissing(issues, clarifications, path, $"Positive numeric parameter '{propertyName}' is required.");
        }
    }

    private static void ValidateNumber(
        CadIntentJsonValue obj,
        string propertyName,
        string path,
        ICollection<ValidationIssue> issues,
        ICollection<string> clarifications)
    {
        var value = GetProperty(obj, propertyName);
        if (value == null || value.Kind != CadIntentJsonValueKind.Number)
        {
            AddMissing(issues, clarifications, path, $"Numeric parameter '{propertyName}' is required.");
        }
    }

    private static DrawingPoint? ValidatePoint(
        CadIntentJsonValue obj,
        string propertyName,
        string path,
        ICollection<ValidationIssue> issues,
        ICollection<string> clarifications)
    {
        var value = GetProperty(obj, propertyName);
        if (value == null || value.Kind != CadIntentJsonValueKind.Array || value.ArrayItems.Count != 2)
        {
            AddMissing(issues, clarifications, path, $"Point parameter '{propertyName}' must be [x, y].");
            return null;
        }

        var x = value.ArrayItems[0];
        var y = value.ArrayItems[1];
        if (x.Kind != CadIntentJsonValueKind.Number || y.Kind != CadIntentJsonValueKind.Number)
        {
            AddMissing(issues, clarifications, path, $"Point parameter '{propertyName}' must contain numeric [x, y] values.");
            return null;
        }

        return new DrawingPoint(x.NumberValue, y.NumberValue);
    }

    private static bool PointsEqual(DrawingPoint a, DrawingPoint b)
    {
        return Math.Abs(a.X - b.X) < PointTolerance
            && Math.Abs(a.Y - b.Y) < PointTolerance;
    }

    private static void AddProfileNotClosed(
        ICollection<ValidationIssue> issues,
        ICollection<string> clarifications,
        string message)
    {
        AddIssue(issues, CadIntentIssueCodes.ProfileNotClosed, "$.profile", message);
        clarifications.Add(message);
    }

    private static void AddMissing(
        ICollection<ValidationIssue> issues,
        ICollection<string> clarifications,
        string path,
        string message)
    {
        AddIssue(issues, CadIntentIssueCodes.MissingRequiredParameter, path, message);
        clarifications.Add(message);
    }

    private static CadJsonInputClassification Create(
        CadJsonInputKind kind,
        IReadOnlyList<ValidationIssue> issues,
        IReadOnlyList<string> clarifications)
    {
        return new CadJsonInputClassification(
            kind,
            issues.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(issues),
            clarifications.ToArray());
    }

    private static CadJsonInputClassification Unsupported(string path, string message)
    {
        return Create(
            CadJsonInputKind.Unsupported,
            new[]
            {
                new ValidationIssue(CadIntentIssueCodes.UnsupportedJsonContract, path, message, ValidationSeverity.Error)
            },
            Array.Empty<string>());
    }

    private static void AddIssue(ICollection<ValidationIssue> issues, string code, string path, string message)
    {
        issues.Add(new ValidationIssue(code, path, message, ValidationSeverity.Error));
    }

    private static bool HasProperty(CadIntentJsonValue obj, string name)
    {
        return obj.ObjectProperties.ContainsKey(name);
    }

    private static CadIntentJsonValue? GetProperty(CadIntentJsonValue obj, string name)
    {
        return obj.ObjectProperties.TryGetValue(name, out var value) ? value : null;
    }

    private static CadIntentJsonValue? ReadObject(CadIntentJsonValue obj, string propertyName)
    {
        var value = GetProperty(obj, propertyName);
        return value != null && value.Kind == CadIntentJsonValueKind.Object ? value : null;
    }

    private static IReadOnlyList<CadIntentJsonValue>? ReadArray(CadIntentJsonValue obj, string propertyName)
    {
        var value = GetProperty(obj, propertyName);
        return value != null && value.Kind == CadIntentJsonValueKind.Array ? value.ArrayItems : null;
    }

    private static string ReadString(CadIntentJsonValue obj, string propertyName)
    {
        var value = GetProperty(obj, propertyName);
        return value != null && value.Kind == CadIntentJsonValueKind.String ? value.StringValue : string.Empty;
    }

    private static bool ReadBoolean(CadIntentJsonValue obj, string propertyName)
    {
        var value = GetProperty(obj, propertyName);
        return value != null && value.Kind == CadIntentJsonValueKind.Boolean && value.BooleanValue;
    }
}

internal sealed class CadIntentJsonParseException : Exception
{
    public CadIntentJsonParseException(string message)
        : base(message)
    {
    }
}

internal enum CadIntentJsonValueKind
{
    Object,
    Array,
    String,
    Number,
    Boolean,
    Null
}

internal sealed class CadIntentJsonValue
{
    private CadIntentJsonValue(CadIntentJsonValueKind kind)
    {
        Kind = kind;
        ObjectProperties = new Dictionary<string, CadIntentJsonValue>(StringComparer.Ordinal);
        ArrayItems = Array.Empty<CadIntentJsonValue>();
        StringValue = string.Empty;
    }

    public CadIntentJsonValueKind Kind { get; }

    public Dictionary<string, CadIntentJsonValue> ObjectProperties { get; private set; }

    public IReadOnlyList<CadIntentJsonValue> ArrayItems { get; private set; }

    public string StringValue { get; private set; }

    public double NumberValue { get; private set; }

    public bool BooleanValue { get; private set; }

    public static CadIntentJsonValue ForObject(Dictionary<string, CadIntentJsonValue> value)
    {
        return new CadIntentJsonValue(CadIntentJsonValueKind.Object)
        {
            ObjectProperties = value
        };
    }

    public static CadIntentJsonValue ForArray(IReadOnlyList<CadIntentJsonValue> value)
    {
        return new CadIntentJsonValue(CadIntentJsonValueKind.Array)
        {
            ArrayItems = value
        };
    }

    public static CadIntentJsonValue ForString(string value)
    {
        return new CadIntentJsonValue(CadIntentJsonValueKind.String)
        {
            StringValue = value
        };
    }

    public static CadIntentJsonValue ForNumber(double value)
    {
        return new CadIntentJsonValue(CadIntentJsonValueKind.Number)
        {
            NumberValue = value
        };
    }

    public static CadIntentJsonValue ForBoolean(bool value)
    {
        return new CadIntentJsonValue(CadIntentJsonValueKind.Boolean)
        {
            BooleanValue = value
        };
    }

    public static CadIntentJsonValue ForNull()
    {
        return new CadIntentJsonValue(CadIntentJsonValueKind.Null);
    }
}

internal sealed class CadIntentJsonParser
{
    private readonly string _json;
    private int _position;

    private CadIntentJsonParser(string json)
    {
        _json = json;
    }

    public static CadIntentJsonValue Parse(string json)
    {
        var parser = new CadIntentJsonParser(json);
        var value = parser.ParseValue();
        parser.SkipWhitespace();

        if (!parser.IsAtEnd)
        {
            throw parser.Error("Unexpected trailing characters.");
        }

        return value;
    }

    private bool IsAtEnd => _position >= _json.Length;

    private CadIntentJsonValue ParseValue()
    {
        SkipWhitespace();

        if (IsAtEnd)
        {
            throw Error("Unexpected end of JSON.");
        }

        var c = _json[_position];
        switch (c)
        {
            case '{':
                return ParseObject();
            case '[':
                return ParseArray();
            case '"':
                return CadIntentJsonValue.ForString(ParseString());
            case 't':
                ConsumeLiteral("true");
                return CadIntentJsonValue.ForBoolean(true);
            case 'f':
                ConsumeLiteral("false");
                return CadIntentJsonValue.ForBoolean(false);
            case 'n':
                ConsumeLiteral("null");
                return CadIntentJsonValue.ForNull();
            default:
                if (c == '-' || char.IsDigit(c))
                {
                    return CadIntentJsonValue.ForNumber(ParseNumber());
                }

                throw Error($"Unexpected character '{c}'.");
        }
    }

    private CadIntentJsonValue ParseObject()
    {
        Expect('{');
        SkipWhitespace();

        var properties = new Dictionary<string, CadIntentJsonValue>(StringComparer.Ordinal);
        if (TryConsume('}'))
        {
            return CadIntentJsonValue.ForObject(properties);
        }

        while (true)
        {
            SkipWhitespace();
            if (IsAtEnd || _json[_position] != '"')
            {
                throw Error("Object property name must be a string.");
            }

            var propertyName = ParseString();
            SkipWhitespace();
            Expect(':');
            var value = ParseValue();

            if (properties.ContainsKey(propertyName))
            {
                throw Error($"Duplicate property '{propertyName}'.");
            }

            properties.Add(propertyName, value);
            SkipWhitespace();

            if (TryConsume('}'))
            {
                break;
            }

            Expect(',');
        }

        return CadIntentJsonValue.ForObject(properties);
    }

    private CadIntentJsonValue ParseArray()
    {
        Expect('[');
        SkipWhitespace();

        var items = new List<CadIntentJsonValue>();
        if (TryConsume(']'))
        {
            return CadIntentJsonValue.ForArray(items);
        }

        while (true)
        {
            items.Add(ParseValue());
            SkipWhitespace();

            if (TryConsume(']'))
            {
                break;
            }

            Expect(',');
        }

        return CadIntentJsonValue.ForArray(items);
    }

    private string ParseString()
    {
        Expect('"');
        var builder = new StringBuilder();

        while (!IsAtEnd)
        {
            var c = _json[_position++];
            if (c == '"')
            {
                return builder.ToString();
            }

            if (c == '\\')
            {
                if (IsAtEnd)
                {
                    throw Error("Unterminated string escape.");
                }

                builder.Append(ParseEscape());
                continue;
            }

            if (c < 0x20)
            {
                throw Error("Control characters are not allowed in JSON strings.");
            }

            builder.Append(c);
        }

        throw Error("Unterminated string.");
    }

    private char ParseEscape()
    {
        var escaped = _json[_position++];
        switch (escaped)
        {
            case '"':
                return '"';
            case '\\':
                return '\\';
            case '/':
                return '/';
            case 'b':
                return '\b';
            case 'f':
                return '\f';
            case 'n':
                return '\n';
            case 'r':
                return '\r';
            case 't':
                return '\t';
            case 'u':
                return ParseUnicodeEscape();
            default:
                throw Error($"Invalid string escape '\\{escaped}'.");
        }
    }

    private char ParseUnicodeEscape()
    {
        if (_position + 4 > _json.Length)
        {
            throw Error("Incomplete unicode escape.");
        }

        var code = 0;
        for (var index = 0; index < 4; index++)
        {
            var value = HexValue(_json[_position++]);
            if (value < 0)
            {
                throw Error("Invalid unicode escape.");
            }

            code = (code << 4) + value;
        }

        return (char)code;
    }

    private double ParseNumber()
    {
        var start = _position;

        TryConsume('-');

        if (TryConsume('0'))
        {
            if (!IsAtEnd && char.IsDigit(_json[_position]))
            {
                throw Error("Leading zeroes are not allowed in JSON numbers.");
            }
        }
        else
        {
            ConsumeDigits();
        }

        if (TryConsume('.'))
        {
            ConsumeDigits();
        }

        if (!IsAtEnd && (_json[_position] == 'e' || _json[_position] == 'E'))
        {
            _position++;
            if (!IsAtEnd && (_json[_position] == '+' || _json[_position] == '-'))
            {
                _position++;
            }

            ConsumeDigits();
        }

        var text = _json.Substring(start, _position - start);
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || double.IsNaN(value)
            || double.IsInfinity(value))
        {
            throw Error($"Invalid number '{text}'.");
        }

        return value;
    }

    private void ConsumeDigits()
    {
        var start = _position;
        while (!IsAtEnd && char.IsDigit(_json[_position]))
        {
            _position++;
        }

        if (_position == start)
        {
            throw Error("Expected digit.");
        }
    }

    private void ConsumeLiteral(string literal)
    {
        if (_position + literal.Length > _json.Length
            || !string.Equals(_json.Substring(_position, literal.Length), literal, StringComparison.Ordinal))
        {
            throw Error($"Expected '{literal}'.");
        }

        _position += literal.Length;
    }

    private void Expect(char expected)
    {
        if (!TryConsume(expected))
        {
            throw Error($"Expected '{expected}'.");
        }
    }

    private bool TryConsume(char expected)
    {
        if (!IsAtEnd && _json[_position] == expected)
        {
            _position++;
            return true;
        }

        return false;
    }

    private void SkipWhitespace()
    {
        while (!IsAtEnd)
        {
            var c = _json[_position];
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
            {
                _position++;
                continue;
            }

            break;
        }
    }

    private CadIntentJsonParseException Error(string message)
    {
        return new CadIntentJsonParseException($"{message} Position {_position}.");
    }

    private static int HexValue(char c)
    {
        if (c >= '0' && c <= '9')
        {
            return c - '0';
        }

        if (c >= 'a' && c <= 'f')
        {
            return c - 'a' + 10;
        }

        if (c >= 'A' && c <= 'F')
        {
            return c - 'A' + 10;
        }

        return -1;
    }
}
