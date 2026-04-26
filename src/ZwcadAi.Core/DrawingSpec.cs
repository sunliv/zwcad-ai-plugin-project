using System;
using System.Collections.Generic;

namespace ZwcadAi.Core;

public sealed class DrawingSpec
{
    public string DrawingSpecVersion { get; set; } = "1.0";

    public string Units { get; set; } = "mm";

    public DrawingMetadata Metadata { get; set; } = new DrawingMetadata();

    public IReadOnlyList<LayerSpec> Layers { get; set; } = Array.Empty<LayerSpec>();

    public IReadOnlyList<EntitySpec> Entities { get; set; } = Array.Empty<EntitySpec>();

    public IReadOnlyList<DimensionSpec> Dimensions { get; set; } = Array.Empty<DimensionSpec>();

    public IReadOnlyList<string> Clarifications { get; set; } = Array.Empty<string>();
}

public sealed class DrawingMetadata
{
    public string Title { get; set; } = string.Empty;

    public string Domain { get; set; } = DrawingDomain.MechanicalPlate;

    public string CreatedBy { get; set; } = string.Empty;

    public string RequestId { get; set; } = string.Empty;
}

public sealed class LayerSpec
{
    public string Name { get; set; } = string.Empty;

    public int Color { get; set; }

    public string LineType { get; set; } = "Continuous";

    public double LineWeight { get; set; }
}

public sealed class EntitySpec
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Layer { get; set; } = string.Empty;
}

public sealed class DimensionSpec
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Layer { get; set; } = string.Empty;
}
