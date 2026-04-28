using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ZwcadAi.Core;

public static class DrawingSpecValidator
{
    private static readonly HashSet<string> AllowedUnits = new HashSet<string>(
        new[] { "mm", "cm", "m", "inch" },
        StringComparer.Ordinal);

    private static readonly HashSet<string> AllowedEntityTypes = new HashSet<string>(
        new[]
        {
            EntityTypes.Line,
            EntityTypes.Polyline,
            EntityTypes.Circle,
            EntityTypes.Arc,
            EntityTypes.Text,
            EntityTypes.MText,
            EntityTypes.CenterMark
        },
        StringComparer.Ordinal);

    private static readonly HashSet<string> AllowedDimensionTypes = new HashSet<string>(
        new[]
        {
            DimensionTypes.Linear,
            DimensionTypes.Aligned,
            DimensionTypes.Radius,
            DimensionTypes.Diameter,
            DimensionTypes.Angular
        },
        StringComparer.Ordinal);

    private static readonly HashSet<string> RootProperties = new HashSet<string>(
        new[] { "drawingSpecVersion", "units", "metadata", "layers", "entities", "dimensions", "clarifications" },
        StringComparer.Ordinal);

    private static readonly HashSet<string> MetadataProperties = new HashSet<string>(
        new[] { "title", "domain", "author", "createdBy", "requestId" },
        StringComparer.Ordinal);

    private static readonly HashSet<string> LayerProperties = new HashSet<string>(
        new[] { "name", "color", "lineType", "lineWeight" },
        StringComparer.Ordinal);

    public static ValidationResult ValidateSchema(DrawingSpec spec)
    {
        var issues = new List<ValidationIssue>();

        if (spec == null)
        {
            AddIssue(issues, "invalid_spec", "$", "DrawingSpec is required.");
            return ValidationResult.Failure(issues);
        }

        ValidateRequiredString(spec.DrawingSpecVersion, "$.drawingSpecVersion", issues);
        ValidateRequiredString(spec.Units, "$.units", issues);

        if (!string.Equals(spec.DrawingSpecVersion, DrawingSpecWireFormat.Version, StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                "invalid_value",
                "$.drawingSpecVersion",
                $"DrawingSpec version must be '{DrawingSpecWireFormat.Version}'.");
        }

        if (!AllowedUnits.Contains(spec.Units))
        {
            AddIssue(issues, "invalid_value", "$.units", $"Unit '{spec.Units}' is not supported by DrawingSpec v1.");
        }

        var layers = spec.Layers;
        if (layers == null)
        {
            AddIssue(issues, "missing_required", "$.layers", "Property 'layers' is required.");
        }
        else
        {
            if (layers.Count == 0)
            {
                AddIssue(issues, "array_too_small", "$.layers", "Property 'layers' must contain at least one layer.");
            }

            for (var index = 0; index < layers.Count; index++)
            {
                var layer = layers[index];
                ValidateRequiredString(layer.Name, $"$.layers[{index}].name", issues);

                if (layer.Color <= 0 || layer.Color > 255)
                {
                    AddIssue(issues, "invalid_value", $"$.layers[{index}].color", "Layer color must be between 1 and 255 when specified.");
                }
            }
        }

        var entities = spec.Entities;
        if (entities == null)
        {
            AddIssue(issues, "missing_required", "$.entities", "Property 'entities' is required.");
        }
        else
        {
            for (var index = 0; index < entities.Count; index++)
            {
                ValidateSchemaEntity(entities[index], $"$.entities[{index}]", issues);
            }
        }

        var dimensions = spec.Dimensions ?? Array.Empty<DimensionSpec>();
        for (var index = 0; index < dimensions.Count; index++)
        {
            ValidateSchemaDimension(dimensions[index], $"$.dimensions[{index}]", issues);
        }

        return ToValidationResult(issues);
    }

    public static ValidationResult ValidateSchemaJson(string json)
    {
        var issues = new List<ValidationIssue>();

        if (string.IsNullOrWhiteSpace(json))
        {
            AddIssue(issues, "invalid_json", "$", "DrawingSpec JSON is required.");
            return ValidationResult.Failure(issues);
        }

        JsonValue root;
        try
        {
            root = JsonParser.Parse(json);
        }
        catch (JsonParseException exception)
        {
            AddIssue(issues, "invalid_json", "$", exception.Message);
            return ValidationResult.Failure(issues);
        }

        ValidateRoot(root, issues);
        return ToValidationResult(issues);
    }

    public static ValidationResult ValidateBusinessRules(DrawingSpec spec)
    {
        return ValidateBusinessRules(spec, DrawingSpecBusinessRuleLimits.Default);
    }

    public static ValidationResult ValidateBusinessRules(DrawingSpec spec, DrawingSpecBusinessRuleLimits limits)
    {
        var issues = new List<ValidationIssue>();

        if (spec == null)
        {
            AddIssue(issues, "invalid_spec", "$", "DrawingSpec is required.");
            return ValidationResult.Failure(issues);
        }

        if (limits == null)
        {
            throw new ArgumentNullException(nameof(limits));
        }

        if (!string.Equals(spec.DrawingSpecVersion, DrawingSpecWireFormat.Version, StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                "unsupported_version",
                "$.drawingSpecVersion",
                $"DrawingSpec version must be '{DrawingSpecWireFormat.Version}'.");
        }

        if (!string.Equals(spec.Units, "mm", StringComparison.Ordinal))
        {
            AddIssue(issues, "unsupported_units", "$.units", "The mechanical_plate MVP business profile only accepts millimeters.");
        }

        if (spec.Metadata != null
            && !string.IsNullOrWhiteSpace(spec.Metadata.Domain)
            && !string.Equals(spec.Metadata.Domain, DrawingDomain.MechanicalPlate, StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                "unsupported_domain",
                "$.metadata.domain",
                $"The MVP business profile only accepts domain '{DrawingDomain.MechanicalPlate}'.");
        }

        ValidateBusinessLayers(spec, issues);

        var entities = spec.Entities ?? Array.Empty<EntitySpec>();
        if (entities.Count > limits.MaxEntities)
        {
            AddIssue(
                issues,
                "entity_count_exceeded",
                "$.entities",
                $"DrawingSpec contains {entities.Count} entities; the limit is {limits.MaxEntities}.");
        }

        var dimensions = spec.Dimensions ?? Array.Empty<DimensionSpec>();
        if (dimensions.Count > limits.MaxDimensions)
        {
            AddIssue(
                issues,
                "dimension_count_exceeded",
                "$.dimensions",
                $"DrawingSpec contains {dimensions.Count} dimensions; the limit is {limits.MaxDimensions}.");
        }

        ValidateBusinessEntities(spec, limits, issues);
        ValidateBusinessDimensions(spec, limits, issues);

        return ToValidationResult(issues);
    }

    public static IReadOnlyList<string> AllowedLayerNames => CadLayerStandards.Definitions.Keys.ToArray();

    private static void ValidateSchemaEntity(EntitySpec entity, string path, ICollection<ValidationIssue> issues)
    {
        if (entity == null)
        {
            AddIssue(issues, "invalid_type", path, "Entity must be an object.");
            return;
        }

        ValidateRequiredString(entity.Id, $"{path}.id", issues);
        ValidateRequiredString(entity.Type, $"{path}.type", issues);
        ValidateRequiredString(entity.Layer, $"{path}.layer", issues);

        if (!string.IsNullOrWhiteSpace(entity.Id) && !IsStableId(entity.Id))
        {
            AddIssue(issues, "invalid_value", $"{path}.id", "Id must use only ASCII letters, digits, '.', '_' or '-'.");
        }

        if (!AllowedEntityTypes.Contains(entity.Type))
        {
            AddIssue(issues, "unsupported_entity_type", $"{path}.type", $"Entity type '{entity.Type}' is not part of DrawingSpec v1.");
            return;
        }

        switch (entity.Type)
        {
            case EntityTypes.Line:
                if (entity.Start == null)
                {
                    AddIssue(issues, "missing_required", $"{path}.start", "Property 'start' is required.");
                }

                if (entity.End == null)
                {
                    AddIssue(issues, "missing_required", $"{path}.end", "Property 'end' is required.");
                }

                break;
            case EntityTypes.Polyline:
                if (entity.Points == null)
                {
                    AddIssue(issues, "missing_required", $"{path}.points", "Property 'points' is required.");
                }
                else if (entity.Points.Count < 2)
                {
                    AddIssue(issues, "array_too_small", $"{path}.points", "Array must contain at least 2 items.");
                }

                break;
            case EntityTypes.Circle:
                if (entity.Center == null)
                {
                    AddIssue(issues, "missing_required", $"{path}.center", "Property 'center' is required.");
                }

                if (entity.Radius <= 0)
                {
                    AddIssue(issues, "invalid_value", $"{path}.radius", "Value must be greater than 0.");
                }

                break;
            case EntityTypes.Arc:
                if (entity.Center == null)
                {
                    AddIssue(issues, "missing_required", $"{path}.center", "Property 'center' is required.");
                }

                if (entity.Radius <= 0)
                {
                    AddIssue(issues, "invalid_value", $"{path}.radius", "Value must be greater than 0.");
                }

                break;
            case EntityTypes.Text:
            case EntityTypes.MText:
                if (entity.Position == null)
                {
                    AddIssue(issues, "missing_required", $"{path}.position", "Property 'position' is required.");
                }

                if (entity.Height <= 0)
                {
                    AddIssue(issues, "invalid_value", $"{path}.height", "Value must be greater than 0.");
                }

                break;
            case EntityTypes.CenterMark:
                if (entity.Center == null)
                {
                    AddIssue(issues, "missing_required", $"{path}.center", "Property 'center' is required.");
                }

                if (entity.Size <= 0)
                {
                    AddIssue(issues, "invalid_value", $"{path}.size", "Value must be greater than 0.");
                }

                break;
        }
    }

    private static void ValidateSchemaDimension(DimensionSpec dimension, string path, ICollection<ValidationIssue> issues)
    {
        if (dimension == null)
        {
            AddIssue(issues, "invalid_type", path, "Dimension must be an object.");
            return;
        }

        ValidateRequiredString(dimension.Id, $"{path}.id", issues);
        ValidateRequiredString(dimension.Type, $"{path}.type", issues);
        ValidateRequiredString(dimension.Layer, $"{path}.layer", issues);

        if (!string.IsNullOrWhiteSpace(dimension.Id) && !IsStableId(dimension.Id))
        {
            AddIssue(issues, "invalid_value", $"{path}.id", "Id must use only ASCII letters, digits, '.', '_' or '-'.");
        }

        if (!AllowedDimensionTypes.Contains(dimension.Type))
        {
            AddIssue(issues, "unsupported_dimension_type", $"{path}.type", $"Dimension type '{dimension.Type}' is not part of DrawingSpec v1.");
            return;
        }

        switch (dimension.Type)
        {
            case DimensionTypes.Linear:
            case DimensionTypes.Aligned:
                if (dimension.From == null)
                {
                    AddIssue(issues, "missing_required", $"{path}.from", "Property 'from' is required.");
                }

                if (dimension.To == null)
                {
                    AddIssue(issues, "missing_required", $"{path}.to", "Property 'to' is required.");
                }

                if (dimension.Offset == null)
                {
                    AddIssue(issues, "missing_required", $"{path}.offset", "Property 'offset' is required.");
                }

                break;
            case DimensionTypes.Radius:
            case DimensionTypes.Diameter:
                if (string.IsNullOrWhiteSpace(dimension.TargetEntityId))
                {
                    AddIssue(issues, "missing_required", $"{path}.targetEntityId", "Property 'targetEntityId' is required.");
                }

                break;
            case DimensionTypes.Angular:
                if (dimension.Center == null)
                {
                    AddIssue(issues, "missing_required", $"{path}.center", "Property 'center' is required.");
                }

                if (dimension.From == null)
                {
                    AddIssue(issues, "missing_required", $"{path}.from", "Property 'from' is required.");
                }

                if (dimension.To == null)
                {
                    AddIssue(issues, "missing_required", $"{path}.to", "Property 'to' is required.");
                }

                if (dimension.Offset == null)
                {
                    AddIssue(issues, "missing_required", $"{path}.offset", "Property 'offset' is required.");
                }

                break;
        }
    }

    private static void ValidateRoot(JsonValue root, ICollection<ValidationIssue> issues)
    {
        if (!RequireObject(root, "$", issues))
        {
            return;
        }

        ValidateAdditionalProperties(root, "$", RootProperties, issues);
        ValidateRequiredProperties(root, "$", new[] { "drawingSpecVersion", "units", "layers", "entities" }, issues);

        ValidateStringConst(root, "drawingSpecVersion", "$.drawingSpecVersion", DrawingSpecWireFormat.Version, issues);
        ValidateStringEnum(root, "units", "$.units", AllowedUnits, issues);

        var metadata = GetProperty(root, "metadata");
        if (metadata != null)
        {
            ValidateMetadata(metadata, "$.metadata", issues);
        }

        var layers = GetProperty(root, "layers");
        if (layers != null)
        {
            ValidateLayers(layers, "$.layers", issues);
        }

        var entities = GetProperty(root, "entities");
        if (entities != null)
        {
            ValidateEntities(entities, "$.entities", issues);
        }

        var dimensions = GetProperty(root, "dimensions");
        if (dimensions != null)
        {
            ValidateDimensions(dimensions, "$.dimensions", issues);
        }

        var clarifications = GetProperty(root, "clarifications");
        if (clarifications != null)
        {
            ValidateStringArray(clarifications, "$.clarifications", issues);
        }
    }

    private static void ValidateMetadata(JsonValue metadata, string path, ICollection<ValidationIssue> issues)
    {
        if (!RequireObject(metadata, path, issues))
        {
            return;
        }

        ValidateAdditionalProperties(metadata, path, MetadataProperties, issues);
        foreach (var property in metadata.ObjectProperties)
        {
            ValidateString(property.Value, $"{path}.{property.Key}", minLength: 0, issues);
        }
    }

    private static void ValidateLayers(JsonValue layers, string path, ICollection<ValidationIssue> issues)
    {
        if (!RequireArray(layers, path, issues))
        {
            return;
        }

        if (layers.ArrayItems.Count == 0)
        {
            AddIssue(issues, "array_too_small", path, "Property 'layers' must contain at least one layer.");
        }

        for (var index = 0; index < layers.ArrayItems.Count; index++)
        {
            ValidateLayer(layers.ArrayItems[index], $"{path}[{index}]", issues);
        }
    }

    private static void ValidateLayer(JsonValue layer, string path, ICollection<ValidationIssue> issues)
    {
        if (!RequireObject(layer, path, issues))
        {
            return;
        }

        ValidateAdditionalProperties(layer, path, LayerProperties, issues);
        ValidateRequiredProperties(layer, path, new[] { "name" }, issues);
        ValidateStringProperty(layer, "name", $"{path}.name", 1, issues);
        ValidateIntegerRangeProperty(layer, "color", $"{path}.color", 1, 255, issues);
        ValidateStringProperty(layer, "lineType", $"{path}.lineType", 0, issues);
        ValidateNumberProperty(layer, "lineWeight", $"{path}.lineWeight", issues);
    }

    private static void ValidateEntities(JsonValue entities, string path, ICollection<ValidationIssue> issues)
    {
        if (!RequireArray(entities, path, issues))
        {
            return;
        }

        for (var index = 0; index < entities.ArrayItems.Count; index++)
        {
            ValidateEntity(entities.ArrayItems[index], $"{path}[{index}]", issues);
        }
    }

    private static void ValidateEntity(JsonValue entity, string path, ICollection<ValidationIssue> issues)
    {
        if (!RequireObject(entity, path, issues))
        {
            return;
        }

        ValidateRequiredProperties(entity, path, new[] { "id", "type", "layer" }, issues);
        ValidateStringProperty(entity, "id", $"{path}.id", 1, issues);
        ValidateStableIdProperty(entity, "id", $"{path}.id", issues);
        ValidateStringProperty(entity, "layer", $"{path}.layer", 1, issues);

        var type = GetStringProperty(entity, "type", $"{path}.type", issues);
        if (type == null)
        {
            return;
        }

        if (!AllowedEntityTypes.Contains(type))
        {
            AddIssue(issues, "unsupported_entity_type", $"{path}.type", $"Entity type '{type}' is not part of DrawingSpec v1.");
            return;
        }

        var allowedProperties = GetEntityProperties(type);
        ValidateAdditionalProperties(entity, path, allowedProperties, issues);

        switch (type)
        {
            case EntityTypes.Line:
                ValidateRequiredProperties(entity, path, new[] { "id", "type", "layer", "start", "end" }, issues);
                ValidatePointProperty(entity, "start", $"{path}.start", issues);
                ValidatePointProperty(entity, "end", $"{path}.end", issues);
                break;
            case EntityTypes.Polyline:
                ValidateRequiredProperties(entity, path, new[] { "id", "type", "layer", "points" }, issues);
                ValidateBooleanProperty(entity, "closed", $"{path}.closed", issues);
                ValidatePointArrayProperty(entity, "points", $"{path}.points", 2, issues);
                break;
            case EntityTypes.Circle:
                ValidateRequiredProperties(entity, path, new[] { "id", "type", "layer", "center", "radius" }, issues);
                ValidatePointProperty(entity, "center", $"{path}.center", issues);
                ValidatePositiveNumberProperty(entity, "radius", $"{path}.radius", issues);
                break;
            case EntityTypes.Arc:
                ValidateRequiredProperties(entity, path, new[] { "id", "type", "layer", "center", "radius", "startAngle", "endAngle" }, issues);
                ValidatePointProperty(entity, "center", $"{path}.center", issues);
                ValidatePositiveNumberProperty(entity, "radius", $"{path}.radius", issues);
                ValidateNumberProperty(entity, "startAngle", $"{path}.startAngle", issues);
                ValidateNumberProperty(entity, "endAngle", $"{path}.endAngle", issues);
                break;
            case EntityTypes.Text:
            case EntityTypes.MText:
                ValidateRequiredProperties(entity, path, new[] { "id", "type", "layer", "position", "value", "height" }, issues);
                ValidatePointProperty(entity, "position", $"{path}.position", issues);
                ValidateStringProperty(entity, "value", $"{path}.value", 0, issues);
                ValidatePositiveNumberProperty(entity, "height", $"{path}.height", issues);
                ValidateNumberProperty(entity, "rotation", $"{path}.rotation", issues);
                break;
            case EntityTypes.CenterMark:
                ValidateRequiredProperties(entity, path, new[] { "id", "type", "layer", "center", "size" }, issues);
                ValidatePointProperty(entity, "center", $"{path}.center", issues);
                ValidatePositiveNumberProperty(entity, "size", $"{path}.size", issues);
                break;
        }
    }

    private static void ValidateDimensions(JsonValue dimensions, string path, ICollection<ValidationIssue> issues)
    {
        if (!RequireArray(dimensions, path, issues))
        {
            return;
        }

        for (var index = 0; index < dimensions.ArrayItems.Count; index++)
        {
            ValidateDimension(dimensions.ArrayItems[index], $"{path}[{index}]", issues);
        }
    }

    private static void ValidateDimension(JsonValue dimension, string path, ICollection<ValidationIssue> issues)
    {
        if (!RequireObject(dimension, path, issues))
        {
            return;
        }

        ValidateRequiredProperties(dimension, path, new[] { "id", "type", "layer" }, issues);
        ValidateStringProperty(dimension, "id", $"{path}.id", 1, issues);
        ValidateStableIdProperty(dimension, "id", $"{path}.id", issues);
        ValidateStringProperty(dimension, "layer", $"{path}.layer", 1, issues);
        ValidateStringEnumProperty(dimension, "type", $"{path}.type", AllowedDimensionTypes, issues);
        ValidatePointProperty(dimension, "from", $"{path}.from", issues);
        ValidatePointProperty(dimension, "to", $"{path}.to", issues);
        ValidatePointProperty(dimension, "center", $"{path}.center", issues);
        ValidatePointProperty(dimension, "offset", $"{path}.offset", issues);
        ValidateStringProperty(dimension, "targetEntityId", $"{path}.targetEntityId", 0, issues);
        ValidateStringProperty(dimension, "text", $"{path}.text", 0, issues);

        var type = GetStringProperty(dimension, "type", $"{path}.type", issues);
        if (type == null || !AllowedDimensionTypes.Contains(type))
        {
            return;
        }

        ValidateAdditionalProperties(dimension, path, GetDimensionProperties(type), issues);

        switch (type)
        {
            case DimensionTypes.Linear:
            case DimensionTypes.Aligned:
                ValidateRequiredProperties(dimension, path, new[] { "from", "to", "offset" }, issues);
                break;
            case DimensionTypes.Radius:
            case DimensionTypes.Diameter:
                ValidateRequiredProperties(dimension, path, new[] { "targetEntityId" }, issues);
                break;
            case DimensionTypes.Angular:
                ValidateRequiredProperties(dimension, path, new[] { "center", "from", "to", "offset" }, issues);
                break;
        }
    }

    private static void ValidateBusinessLayers(DrawingSpec spec, ICollection<ValidationIssue> issues)
    {
        var layers = spec.Layers ?? Array.Empty<LayerSpec>();
        var layerNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var layer in layers)
        {
            if (layer == null)
            {
                AddIssue(issues, "invalid_type", "$.layers[?]", "Layer must be an object.");
                continue;
            }

            var name = layer.Name ?? string.Empty;
            var path = $"$.layers[{(string.IsNullOrWhiteSpace(name) ? "?" : name)}].name";

            if (string.IsNullOrWhiteSpace(name))
            {
                AddIssue(issues, "missing_layer_name", path, "Layer name is required.");
                continue;
            }

            if (!layerNames.Add(name))
            {
                AddIssue(issues, "duplicate_layer", path, $"Layer '{name}' is declared more than once.");
            }

            if (!CadLayerStandards.TryGet(name, out var standard))
            {
                AddIssue(issues, "unsupported_layer", path, $"Layer '{name}' is outside enterprise-default-v1.");
                continue;
            }

            ValidateLayerStandard(layer, standard, issues);
        }

        foreach (var requiredLayer in CadLayerStandards.RequiredProductionLayerNames)
        {
            if (!layerNames.Contains(requiredLayer))
            {
                AddIssue(
                    issues,
                    "missing_required_layer",
                    $"$.layers[{requiredLayer}]",
                    $"Layer '{requiredLayer}' is required for production DrawingSpec validation.");
            }
        }
    }

    private static void ValidateLayerStandard(
        LayerSpec layer,
        CadLayerStandard standard,
        ICollection<ValidationIssue> issues)
    {
        var pathPrefix = $"$.layers[{standard.Name}]";

        if (layer.Color != standard.Color)
        {
            AddIssue(
                issues,
                "invalid_layer_color",
                $"{pathPrefix}.color",
                $"Layer '{standard.Name}' must use color index {standard.Color}.");
        }

        if (!string.Equals(layer.LineType, standard.LineType, StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                "invalid_layer_linetype",
                $"{pathPrefix}.lineType",
                $"Layer '{standard.Name}' must use line type '{standard.LineType}'.");
        }

        if (Math.Abs(layer.LineWeight - standard.LineWeight) > 0.000001)
        {
            AddIssue(
                issues,
                "invalid_layer_lineweight",
                $"{pathPrefix}.lineWeight",
                $"Layer '{standard.Name}' must use line weight {standard.LineWeight:0.###} mm.");
        }
    }

    private static void ValidateBusinessEntities(DrawingSpec spec, DrawingSpecBusinessRuleLimits limits, ICollection<ValidationIssue> issues)
    {
        var entities = spec.Entities ?? Array.Empty<EntitySpec>();
        var layers = new HashSet<string>((spec.Layers ?? Array.Empty<LayerSpec>()).Select(layer => layer.Name), StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entity in entities)
        {
            var path = EntityPath(entity);

            if (string.IsNullOrWhiteSpace(entity.Id))
            {
                AddIssue(issues, "missing_entity_id", $"{path}.id", "Entity id is required.");
            }
            else if (!ids.Add(entity.Id))
            {
                AddIssue(issues, "duplicate_entity_id", $"{path}.id", $"Entity id '{entity.Id}' is not unique.");
            }

            if (!IsStableId(entity.Id))
            {
                AddIssue(issues, "invalid_entity_id", $"{path}.id", $"Entity id '{entity.Id}' must be stable ASCII using letters, digits, '.', '_' or '-'.");
            }

            if (!AllowedEntityTypes.Contains(entity.Type))
            {
                AddIssue(issues, "unsupported_entity_type", $"{path}.type", $"Entity type '{entity.Type}' is not part of DrawingSpec v1.");
            }

            if (!layers.Contains(entity.Layer))
            {
                AddIssue(issues, "missing_entity_layer", $"{path}.layer", $"Entity '{entity.Id}' references missing layer '{entity.Layer}'.");
            }

            ValidateEntityLayerRule(entity, path, issues);
            ValidateEntityGeometryRule(entity, path, limits, issues);
        }
    }

    private static void ValidateBusinessDimensions(DrawingSpec spec, DrawingSpecBusinessRuleLimits limits, ICollection<ValidationIssue> issues)
    {
        var dimensions = spec.Dimensions ?? Array.Empty<DimensionSpec>();
        var layers = new HashSet<string>((spec.Layers ?? Array.Empty<LayerSpec>()).Select(layer => layer.Name), StringComparer.Ordinal);
        var entityIds = new HashSet<string>((spec.Entities ?? Array.Empty<EntitySpec>()).Select(entity => entity.Id), StringComparer.Ordinal);
        var dimensionIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var dimension in dimensions)
        {
            var path = DimensionPath(dimension);

            if (string.IsNullOrWhiteSpace(dimension.Id))
            {
                AddIssue(issues, "missing_dimension_id", $"{path}.id", "Dimension id is required.");
            }
            else if (!dimensionIds.Add(dimension.Id))
            {
                AddIssue(issues, "duplicate_dimension_id", $"{path}.id", $"Dimension id '{dimension.Id}' is not unique.");
            }

            if (!IsStableId(dimension.Id))
            {
                AddIssue(issues, "invalid_dimension_id", $"{path}.id", $"Dimension id '{dimension.Id}' must be stable ASCII using letters, digits, '.', '_' or '-'.");
            }

            if (!AllowedDimensionTypes.Contains(dimension.Type))
            {
                AddIssue(issues, "unsupported_dimension_type", $"{path}.type", $"Dimension type '{dimension.Type}' is not part of DrawingSpec v1.");
            }

            if (!layers.Contains(dimension.Layer))
            {
                AddIssue(issues, "missing_dimension_layer", $"{path}.layer", $"Dimension '{dimension.Id}' references missing layer '{dimension.Layer}'.");
            }

            if (!string.Equals(dimension.Layer, CadLayerNames.Dimension, StringComparison.Ordinal))
            {
                AddIssue(issues, "invalid_dimension_layer", $"{path}.layer", $"Dimension '{dimension.Id}' must be on layer '{CadLayerNames.Dimension}'.");
            }

            if ((string.Equals(dimension.Type, DimensionTypes.Linear, StringComparison.Ordinal)
                    || string.Equals(dimension.Type, DimensionTypes.Aligned, StringComparison.Ordinal))
                && (dimension.From == null || dimension.To == null || dimension.Offset == null))
            {
                AddIssue(issues, "missing_dimension_geometry", path, $"Dimension '{dimension.Id}' must include from, to, and offset points.");
            }

            if ((string.Equals(dimension.Type, DimensionTypes.Radius, StringComparison.Ordinal)
                    || string.Equals(dimension.Type, DimensionTypes.Diameter, StringComparison.Ordinal))
                && string.IsNullOrWhiteSpace(dimension.TargetEntityId))
            {
                AddIssue(issues, "missing_dimension_target", $"{path}.targetEntityId", $"Dimension '{dimension.Id}' must reference targetEntityId.");
            }

            if (string.Equals(dimension.Type, DimensionTypes.Angular, StringComparison.Ordinal)
                && (dimension.Center == null || dimension.From == null || dimension.To == null || dimension.Offset == null))
            {
                AddIssue(issues, "missing_dimension_geometry", path, $"Dimension '{dimension.Id}' must include center, from, to, and offset points.");
            }

            if (!string.IsNullOrWhiteSpace(dimension.TargetEntityId) && !entityIds.Contains(dimension.TargetEntityId))
            {
                AddIssue(issues, "missing_dimension_target", $"{path}.targetEntityId", $"Dimension '{dimension.Id}' targets missing entity '{dimension.TargetEntityId}'.");
            }

            ValidatePointRange(dimension.From, $"{path}.from", limits, issues);
            ValidatePointRange(dimension.To, $"{path}.to", limits, issues);
            ValidatePointRange(dimension.Center, $"{path}.center", limits, issues);
            ValidatePointRange(dimension.Offset, $"{path}.offset", limits, issues);
        }
    }

    private static void ValidateEntityLayerRule(EntitySpec entity, string path, ICollection<ValidationIssue> issues)
    {
        if (string.Equals(entity.Type, EntityTypes.CenterMark, StringComparison.Ordinal)
            && !string.Equals(entity.Layer, CadLayerNames.Center, StringComparison.Ordinal))
        {
            AddIssue(issues, "invalid_center_mark_layer", $"{path}.layer", $"Center mark '{entity.Id}' must be on layer '{CadLayerNames.Center}'.");
        }

        if ((string.Equals(entity.Type, EntityTypes.Text, StringComparison.Ordinal)
                || string.Equals(entity.Type, EntityTypes.MText, StringComparison.Ordinal))
            && !string.Equals(entity.Layer, CadLayerNames.Text, StringComparison.Ordinal)
            && !string.Equals(entity.Layer, CadLayerNames.Title, StringComparison.Ordinal))
        {
            AddIssue(issues, "invalid_text_layer", $"{path}.layer", $"Text entity '{entity.Id}' must be on layer 'TEXT' or 'TITLE'.");
        }
    }

    private static void ValidateEntityGeometryRule(
        EntitySpec entity,
        string path,
        DrawingSpecBusinessRuleLimits limits,
        ICollection<ValidationIssue> issues)
    {
        switch (entity.Type)
        {
            case EntityTypes.Line:
                if (entity.Start == null || entity.End == null)
                {
                    AddIssue(issues, "invalid_line_geometry", path, $"Line '{entity.Id}' must include start and end points.");
                }

                ValidatePointRange(entity.Start, $"{path}.start", limits, issues);
                ValidatePointRange(entity.End, $"{path}.end", limits, issues);
                break;
            case EntityTypes.Polyline:
                if (entity.Points == null || entity.Points.Count < 2)
                {
                    AddIssue(issues, "invalid_polyline_points", $"{path}.points", $"Polyline '{entity.Id}' must contain at least two points.");
                }

                ValidatePointRange(entity.Points, $"{path}.points", limits, issues);
                break;
            case EntityTypes.Circle:
                if (entity.Center == null || entity.Radius <= 0)
                {
                    AddIssue(issues, "invalid_circle_geometry", path, $"Circle '{entity.Id}' must include center and positive radius.");
                }

                ValidatePointRange(entity.Center, $"{path}.center", limits, issues);
                ValidatePositiveLimit(entity.Radius, $"{path}.radius", "radius_out_of_range", limits.MaxSize, issues);
                break;
            case EntityTypes.Arc:
                if (entity.Center == null || entity.Radius <= 0)
                {
                    AddIssue(issues, "invalid_arc_geometry", path, $"Arc '{entity.Id}' must include center and positive radius.");
                }

                ValidatePointRange(entity.Center, $"{path}.center", limits, issues);
                ValidatePositiveLimit(entity.Radius, $"{path}.radius", "radius_out_of_range", limits.MaxSize, issues);
                break;
            case EntityTypes.CenterMark:
                if (entity.Center == null || entity.Size <= 0)
                {
                    AddIssue(issues, "invalid_center_mark_geometry", path, $"Center mark '{entity.Id}' must include center and positive size.");
                }

                ValidatePointRange(entity.Center, $"{path}.center", limits, issues);
                ValidatePositiveLimit(entity.Size, $"{path}.size", "size_out_of_range", limits.MaxSize, issues);
                break;
            case EntityTypes.Text:
            case EntityTypes.MText:
                if (entity.Position == null || entity.Height <= 0)
                {
                    AddIssue(issues, "invalid_text_geometry", path, $"Text entity '{entity.Id}' must include position and positive height.");
                }

                ValidatePointRange(entity.Position, $"{path}.position", limits, issues);
                ValidatePositiveLimit(entity.Height, $"{path}.height", "text_height_out_of_range", limits.MaxTextHeight, issues);
                break;
        }
    }

    private static void ValidatePointRange(
        IReadOnlyList<DrawingPoint>? points,
        string path,
        DrawingSpecBusinessRuleLimits limits,
        ICollection<ValidationIssue> issues)
    {
        if (points == null)
        {
            return;
        }

        for (var index = 0; index < points.Count; index++)
        {
            ValidatePointRange(points[index], $"{path}[{index}]", limits, issues);
        }
    }

    private static void ValidatePointRange(
        DrawingPoint? point,
        string path,
        DrawingSpecBusinessRuleLimits limits,
        ICollection<ValidationIssue> issues)
    {
        if (point == null)
        {
            return;
        }

        if (Math.Abs(point.X) > limits.MaxCoordinate || Math.Abs(point.Y) > limits.MaxCoordinate)
        {
            AddIssue(
                issues,
                "coordinate_out_of_range",
                path,
                $"Point ({point.X.ToString(CultureInfo.InvariantCulture)}, {point.Y.ToString(CultureInfo.InvariantCulture)}) exceeds +/-{limits.MaxCoordinate.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static void ValidatePositiveLimit(
        double value,
        string path,
        string code,
        double maxValue,
        ICollection<ValidationIssue> issues)
    {
        if (value <= 0 || value > maxValue)
        {
            AddIssue(issues, code, path, $"Value must be greater than 0 and less than or equal to {maxValue.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static void ValidateStringArray(JsonValue value, string path, ICollection<ValidationIssue> issues)
    {
        if (!RequireArray(value, path, issues))
        {
            return;
        }

        for (var index = 0; index < value.ArrayItems.Count; index++)
        {
            ValidateString(value.ArrayItems[index], $"{path}[{index}]", minLength: 0, issues);
        }
    }

    private static void ValidatePointArrayProperty(
        JsonValue obj,
        string propertyName,
        string path,
        int minItems,
        ICollection<ValidationIssue> issues)
    {
        var value = GetProperty(obj, propertyName);
        if (value == null)
        {
            return;
        }

        if (!RequireArray(value, path, issues))
        {
            return;
        }

        if (value.ArrayItems.Count < minItems)
        {
            AddIssue(issues, "array_too_small", path, $"Array must contain at least {minItems} items.");
        }

        for (var index = 0; index < value.ArrayItems.Count; index++)
        {
            ValidatePoint(value.ArrayItems[index], $"{path}[{index}]", issues);
        }
    }

    private static void ValidatePointProperty(JsonValue obj, string propertyName, string path, ICollection<ValidationIssue> issues)
    {
        var value = GetProperty(obj, propertyName);
        if (value != null)
        {
            ValidatePoint(value, path, issues);
        }
    }

    private static void ValidatePoint(JsonValue value, string path, ICollection<ValidationIssue> issues)
    {
        if (!RequireArray(value, path, issues, "invalid_point2d", $"DrawingSpec v1 point2d wire format is {DrawingSpecWireFormat.Point2d}."))
        {
            return;
        }

        if (value.ArrayItems.Count != 2)
        {
            AddIssue(issues, "invalid_point2d", path, $"DrawingSpec v1 point2d must contain exactly two numbers as {DrawingSpecWireFormat.Point2d}.");
            return;
        }

        ValidateNumber(value.ArrayItems[0], $"{path}[0]", issues);
        ValidateNumber(value.ArrayItems[1], $"{path}[1]", issues);
    }

    private static void ValidateStringProperty(
        JsonValue obj,
        string propertyName,
        string path,
        int minLength,
        ICollection<ValidationIssue> issues)
    {
        var value = GetProperty(obj, propertyName);
        if (value != null)
        {
            ValidateString(value, path, minLength, issues);
        }
    }

    private static void ValidateStableIdProperty(
        JsonValue obj,
        string propertyName,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var value = GetProperty(obj, propertyName);
        if (value == null || value.Kind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.StringValue))
        {
            return;
        }

        if (!IsStableId(value.StringValue))
        {
            AddIssue(issues, "invalid_value", path, "Id must use only ASCII letters, digits, '.', '_' or '-'.");
        }
    }

    private static void ValidateRequiredString(string value, string path, ICollection<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddIssue(issues, "missing_required", path, "Required string value is missing.");
        }
    }

    private static string? GetStringProperty(
        JsonValue obj,
        string propertyName,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var value = GetProperty(obj, propertyName);
        if (value == null)
        {
            return null;
        }

        if (value.Kind != JsonValueKind.String)
        {
            AddIssue(issues, "invalid_type", path, "Value must be a string.");
            return null;
        }

        return value.StringValue;
    }

    private static void ValidateStringConst(
        JsonValue obj,
        string propertyName,
        string path,
        string expected,
        ICollection<ValidationIssue> issues)
    {
        var value = GetStringProperty(obj, propertyName, path, issues);
        if (value != null && !string.Equals(value, expected, StringComparison.Ordinal))
        {
            AddIssue(issues, "invalid_value", path, $"Value must be '{expected}'.");
        }
    }

    private static void ValidateStringEnum(
        JsonValue obj,
        string propertyName,
        string path,
        HashSet<string> allowedValues,
        ICollection<ValidationIssue> issues)
    {
        var value = GetStringProperty(obj, propertyName, path, issues);
        if (value != null && !allowedValues.Contains(value))
        {
            AddIssue(issues, "invalid_value", path, $"Value '{value}' is not allowed.");
        }
    }

    private static void ValidateStringEnumProperty(
        JsonValue obj,
        string propertyName,
        string path,
        HashSet<string> allowedValues,
        ICollection<ValidationIssue> issues)
    {
        var value = GetProperty(obj, propertyName);
        if (value == null)
        {
            return;
        }

        ValidateStringEnum(obj, propertyName, path, allowedValues, issues);
    }

    private static void ValidateString(JsonValue value, string path, int minLength, ICollection<ValidationIssue> issues)
    {
        if (value.Kind != JsonValueKind.String)
        {
            AddIssue(issues, "invalid_type", path, "Value must be a string.");
            return;
        }

        if (value.StringValue.Length < minLength)
        {
            AddIssue(issues, "string_too_short", path, $"String must contain at least {minLength} character(s).");
        }
    }

    private static void ValidateNumberProperty(JsonValue obj, string propertyName, string path, ICollection<ValidationIssue> issues)
    {
        var value = GetProperty(obj, propertyName);
        if (value != null)
        {
            ValidateNumber(value, path, issues);
        }
    }

    private static void ValidatePositiveNumberProperty(JsonValue obj, string propertyName, string path, ICollection<ValidationIssue> issues)
    {
        var value = GetProperty(obj, propertyName);
        if (value == null)
        {
            return;
        }

        if (!ValidateNumber(value, path, issues))
        {
            return;
        }

        if (value.NumberValue <= 0)
        {
            AddIssue(issues, "invalid_value", path, "Value must be greater than 0.");
        }
    }

    private static void ValidateIntegerRangeProperty(
        JsonValue obj,
        string propertyName,
        string path,
        int minimum,
        int maximum,
        ICollection<ValidationIssue> issues)
    {
        var value = GetProperty(obj, propertyName);
        if (value == null)
        {
            return;
        }

        if (!ValidateNumber(value, path, issues))
        {
            return;
        }

        if (Math.Abs(value.NumberValue - Math.Truncate(value.NumberValue)) > 0.000001)
        {
            AddIssue(issues, "invalid_type", path, "Value must be an integer.");
            return;
        }

        if (value.NumberValue < minimum || value.NumberValue > maximum)
        {
            AddIssue(issues, "invalid_value", path, $"Value must be between {minimum} and {maximum}.");
        }
    }

    private static void ValidateBooleanProperty(JsonValue obj, string propertyName, string path, ICollection<ValidationIssue> issues)
    {
        var value = GetProperty(obj, propertyName);
        if (value != null && value.Kind != JsonValueKind.Boolean)
        {
            AddIssue(issues, "invalid_type", path, "Value must be a boolean.");
        }
    }

    private static bool ValidateNumber(JsonValue value, string path, ICollection<ValidationIssue> issues)
    {
        if (value.Kind != JsonValueKind.Number)
        {
            AddIssue(issues, "invalid_type", path, "Value must be a number.");
            return false;
        }

        return true;
    }

    private static void ValidateAdditionalProperties(
        JsonValue obj,
        string path,
        HashSet<string> allowedProperties,
        ICollection<ValidationIssue> issues)
    {
        foreach (var property in obj.ObjectProperties.Keys)
        {
            if (!allowedProperties.Contains(property))
            {
                AddIssue(issues, "unknown_property", $"{path}.{property}", $"Property '{property}' is not allowed here.");
            }
        }
    }

    private static void ValidateRequiredProperties(
        JsonValue obj,
        string path,
        IEnumerable<string> requiredProperties,
        ICollection<ValidationIssue> issues)
    {
        foreach (var property in requiredProperties)
        {
            if (!obj.ObjectProperties.ContainsKey(property))
            {
                AddIssue(issues, "missing_required", $"{path}.{property}", $"Property '{property}' is required.");
            }
        }
    }

    private static bool RequireObject(JsonValue value, string path, ICollection<ValidationIssue> issues)
    {
        if (value.Kind != JsonValueKind.Object)
        {
            AddIssue(issues, "invalid_type", path, "Value must be an object.");
            return false;
        }

        return true;
    }

    private static bool RequireArray(JsonValue value, string path, ICollection<ValidationIssue> issues)
    {
        return RequireArray(value, path, issues, "invalid_type", "Value must be an array.");
    }

    private static bool RequireArray(JsonValue value, string path, ICollection<ValidationIssue> issues, string code, string message)
    {
        if (value.Kind != JsonValueKind.Array)
        {
            AddIssue(issues, code, path, message);
            return false;
        }

        return true;
    }

    private static JsonValue? GetProperty(JsonValue obj, string name)
    {
        return obj.ObjectProperties.TryGetValue(name, out var value) ? value : null;
    }

    private static HashSet<string> GetEntityProperties(string entityType)
    {
        switch (entityType)
        {
            case EntityTypes.Line:
                return new HashSet<string>(new[] { "id", "type", "layer", "start", "end" }, StringComparer.Ordinal);
            case EntityTypes.Polyline:
                return new HashSet<string>(new[] { "id", "type", "layer", "closed", "points" }, StringComparer.Ordinal);
            case EntityTypes.Circle:
                return new HashSet<string>(new[] { "id", "type", "layer", "center", "radius" }, StringComparer.Ordinal);
            case EntityTypes.Arc:
                return new HashSet<string>(new[] { "id", "type", "layer", "center", "radius", "startAngle", "endAngle" }, StringComparer.Ordinal);
            case EntityTypes.Text:
            case EntityTypes.MText:
                return new HashSet<string>(new[] { "id", "type", "layer", "position", "value", "height", "rotation" }, StringComparer.Ordinal);
            case EntityTypes.CenterMark:
                return new HashSet<string>(new[] { "id", "type", "layer", "center", "size" }, StringComparer.Ordinal);
            default:
                return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private static HashSet<string> GetDimensionProperties(string dimensionType)
    {
        switch (dimensionType)
        {
            case DimensionTypes.Linear:
            case DimensionTypes.Aligned:
                return new HashSet<string>(new[] { "id", "type", "layer", "from", "to", "offset", "text" }, StringComparer.Ordinal);
            case DimensionTypes.Radius:
            case DimensionTypes.Diameter:
                return new HashSet<string>(new[] { "id", "type", "layer", "targetEntityId", "text" }, StringComparer.Ordinal);
            case DimensionTypes.Angular:
                return new HashSet<string>(new[] { "id", "type", "layer", "center", "from", "to", "offset", "text" }, StringComparer.Ordinal);
            default:
                return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private static bool IsStableId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var c in value)
        {
            if ((c >= 'a' && c <= 'z')
                || (c >= 'A' && c <= 'Z')
                || (c >= '0' && c <= '9')
                || c == '-'
                || c == '_'
                || c == '.')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static string EntityPath(EntitySpec entity)
    {
        return $"$.entities[{(string.IsNullOrWhiteSpace(entity.Id) ? "?" : entity.Id)}]";
    }

    private static string DimensionPath(DimensionSpec dimension)
    {
        return $"$.dimensions[{(string.IsNullOrWhiteSpace(dimension.Id) ? "?" : dimension.Id)}]";
    }

    private static ValidationResult ToValidationResult(IReadOnlyList<ValidationIssue> issues)
    {
        return issues.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(issues);
    }

    private static void AddIssue(ICollection<ValidationIssue> issues, string code, string path, string message)
    {
        issues.Add(new ValidationIssue(code, path, message, ValidationSeverity.Error));
    }

    private sealed class JsonParser
    {
        private readonly string _json;
        private int _position;

        private JsonParser(string json)
        {
            _json = json;
        }

        public static JsonValue Parse(string json)
        {
            var parser = new JsonParser(json);
            var value = parser.ParseValue();
            parser.SkipWhitespace();

            if (!parser.IsAtEnd)
            {
                throw parser.Error("Unexpected trailing characters.");
            }

            return value;
        }

        private bool IsAtEnd => _position >= _json.Length;

        private JsonValue ParseValue()
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
                    return JsonValue.ForString(ParseString());
                case 't':
                    ConsumeLiteral("true");
                    return JsonValue.ForBoolean(true);
                case 'f':
                    ConsumeLiteral("false");
                    return JsonValue.ForBoolean(false);
                case 'n':
                    ConsumeLiteral("null");
                    return JsonValue.ForNull();
                default:
                    if (c == '-' || char.IsDigit(c))
                    {
                        return JsonValue.ForNumber(ParseNumber());
                    }

                    throw Error($"Unexpected character '{c}'.");
            }
        }

        private JsonValue ParseObject()
        {
            Expect('{');
            SkipWhitespace();

            var properties = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
            if (TryConsume('}'))
            {
                return JsonValue.ForObject(properties);
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

            return JsonValue.ForObject(properties);
        }

        private JsonValue ParseArray()
        {
            Expect('[');
            SkipWhitespace();

            var items = new List<JsonValue>();
            if (TryConsume(']'))
            {
                return JsonValue.ForArray(items);
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

            return JsonValue.ForArray(items);
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

        private JsonParseException Error(string message)
        {
            return new JsonParseException($"{message} Position {_position}.");
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

    private sealed class JsonParseException : Exception
    {
        public JsonParseException(string message)
            : base(message)
        {
        }
    }

    private sealed class JsonValue
    {
        private JsonValue(JsonValueKind kind)
        {
            Kind = kind;
            ObjectProperties = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
            ArrayItems = Array.Empty<JsonValue>();
            StringValue = string.Empty;
        }

        public JsonValueKind Kind { get; }

        public Dictionary<string, JsonValue> ObjectProperties { get; private set; }

        public IReadOnlyList<JsonValue> ArrayItems { get; private set; }

        public string StringValue { get; private set; }

        public double NumberValue { get; private set; }

        public bool BooleanValue { get; private set; }

        public static JsonValue ForObject(Dictionary<string, JsonValue> value)
        {
            return new JsonValue(JsonValueKind.Object)
            {
                ObjectProperties = value
            };
        }

        public static JsonValue ForArray(IReadOnlyList<JsonValue> value)
        {
            return new JsonValue(JsonValueKind.Array)
            {
                ArrayItems = value
            };
        }

        public static JsonValue ForString(string value)
        {
            return new JsonValue(JsonValueKind.String)
            {
                StringValue = value
            };
        }

        public static JsonValue ForNumber(double value)
        {
            return new JsonValue(JsonValueKind.Number)
            {
                NumberValue = value
            };
        }

        public static JsonValue ForBoolean(bool value)
        {
            return new JsonValue(JsonValueKind.Boolean)
            {
                BooleanValue = value
            };
        }

        public static JsonValue ForNull()
        {
            return new JsonValue(JsonValueKind.Null);
        }
    }

    private enum JsonValueKind
    {
        Object,
        Array,
        String,
        Number,
        Boolean,
        Null
    }
}

public sealed class DrawingSpecBusinessRuleLimits
{
    public const int DefaultMaxEntities = 500;
    public const int DefaultMaxDimensions = 500;
    public const double DefaultMaxCoordinate = 100000;
    public const double DefaultMaxSize = 100000;
    public const double DefaultMaxTextHeight = 1000;

    public static DrawingSpecBusinessRuleLimits Default { get; } = new DrawingSpecBusinessRuleLimits();

    public int MaxEntities { get; set; } = DefaultMaxEntities;

    public int MaxDimensions { get; set; } = DefaultMaxDimensions;

    public double MaxCoordinate { get; set; } = DefaultMaxCoordinate;

    public double MaxSize { get; set; } = DefaultMaxSize;

    public double MaxTextHeight { get; set; } = DefaultMaxTextHeight;
}
