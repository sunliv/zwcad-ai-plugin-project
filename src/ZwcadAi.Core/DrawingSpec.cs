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

    public string Author { get; set; } = string.Empty;

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

    public bool Closed { get; set; }

    public IReadOnlyList<DrawingPoint> Points { get; set; } = Array.Empty<DrawingPoint>();

    public DrawingPoint? Start { get; set; }

    public DrawingPoint? End { get; set; }

    public DrawingPoint? Center { get; set; }

    public DrawingPoint? Position { get; set; }

    public double Radius { get; set; }

    public double Size { get; set; }

    public double StartAngle { get; set; }

    public double EndAngle { get; set; }

    public string Value { get; set; } = string.Empty;

    public double Height { get; set; }

    public double Rotation { get; set; }
}

public sealed class DimensionSpec
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Layer { get; set; } = string.Empty;

    public DrawingPoint? From { get; set; }

    public DrawingPoint? To { get; set; }

    public DrawingPoint? Center { get; set; }

    public string TargetEntityId { get; set; } = string.Empty;

    public DrawingPoint? Offset { get; set; }

    public string Text { get; set; } = string.Empty;
}

public sealed class DrawingPoint
{
    public DrawingPoint()
    {
    }

    public DrawingPoint(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; set; }

    public double Y { get; set; }
}

public static class DrawingSpecWireFormat
{
    public const string Version = "1.0";

    // Public DrawingSpec JSON uses point2d arrays: [x, y].
    // DrawingPoint is the internal object model used by Core and renderer code.
    public const string Point2d = "[x, y]";
}

public static class EntityTypes
{
    public const string Line = "line";
    public const string Polyline = "polyline";
    public const string Circle = "circle";
    public const string Arc = "arc";
    public const string Text = "text";
    public const string MText = "mtext";
    public const string CenterMark = "centerMark";
}

public static class DimensionTypes
{
    public const string Linear = "linear";
    public const string Aligned = "aligned";
    public const string Radius = "radius";
    public const string Diameter = "diameter";
    public const string Angular = "angular";
}
