using System;

namespace ZwcadAi.Core;

public static class RectangularPlateSample
{
    public static DrawingSpec Create()
    {
        return new DrawingSpec
        {
            DrawingSpecVersion = "1.0",
            Units = "mm",
            Metadata = new DrawingMetadata
            {
                Title = "100x60 rectangular plate with one hole",
                Domain = DrawingDomain.MechanicalPlate,
                CreatedBy = "poc-fixed-sample",
                RequestId = "p1-03-fixed-rectangular-plate"
            },
            Layers = new[]
            {
                new LayerSpec
                {
                    Name = CadLayerNames.Outline,
                    Color = 7,
                    LineType = "Continuous",
                    LineWeight = 0.35
                },
                new LayerSpec
                {
                    Name = CadLayerNames.Center,
                    Color = 1,
                    LineType = "Center",
                    LineWeight = 0.18
                },
                new LayerSpec
                {
                    Name = CadLayerNames.Dimension,
                    Color = 3,
                    LineType = "Continuous",
                    LineWeight = 0.18
                }
            },
            Entities = new[]
            {
                new EntitySpec
                {
                    Id = "outer-profile",
                    Type = EntityTypes.Polyline,
                    Layer = CadLayerNames.Outline,
                    Closed = true,
                    Points = new[]
                    {
                        new DrawingPoint(0, 0),
                        new DrawingPoint(100, 0),
                        new DrawingPoint(100, 60),
                        new DrawingPoint(0, 60)
                    }
                },
                new EntitySpec
                {
                    Id = "hole-1",
                    Type = EntityTypes.Circle,
                    Layer = CadLayerNames.Outline,
                    Center = new DrawingPoint(30, 30),
                    Radius = 6
                },
                new EntitySpec
                {
                    Id = "hole-1-center",
                    Type = EntityTypes.CenterMark,
                    Layer = CadLayerNames.Center,
                    Center = new DrawingPoint(30, 30),
                    Size = 10
                }
            },
            Dimensions = new[]
            {
                new DimensionSpec
                {
                    Id = "dim-width",
                    Type = DimensionTypes.Linear,
                    Layer = CadLayerNames.Dimension,
                    From = new DrawingPoint(0, 0),
                    To = new DrawingPoint(100, 0),
                    Offset = new DrawingPoint(0, -12),
                    Text = "100"
                },
                new DimensionSpec
                {
                    Id = "dim-height",
                    Type = DimensionTypes.Linear,
                    Layer = CadLayerNames.Dimension,
                    From = new DrawingPoint(100, 0),
                    To = new DrawingPoint(100, 60),
                    Offset = new DrawingPoint(12, 0),
                    Text = "60"
                },
                new DimensionSpec
                {
                    Id = "dim-hole-dia",
                    Type = DimensionTypes.Diameter,
                    Layer = CadLayerNames.Dimension,
                    TargetEntityId = "hole-1",
                    Text = "%%c12"
                }
            },
            Clarifications = Array.Empty<string>()
        };
    }
}
