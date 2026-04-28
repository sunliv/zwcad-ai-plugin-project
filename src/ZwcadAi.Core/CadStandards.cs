using System;
using System.Collections.Generic;
using System.Linq;

namespace ZwcadAi.Core;

public static class CadLayerNames
{
    public const string Outline = "OUTLINE";
    public const string Center = "CENTER";
    public const string Dimension = "DIM";
    public const string Text = "TEXT";
    public const string Hidden = "HIDDEN";
    public const string Construction = "CONSTRUCTION";
    public const string Title = "TITLE";
}

public sealed class CadLayerStandard
{
    public CadLayerStandard(string name, int color, string lineType, double lineWeight)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Color = color;
        LineType = lineType ?? throw new ArgumentNullException(nameof(lineType));
        LineWeight = lineWeight;
    }

    public string Name { get; }

    public int Color { get; }

    public string LineType { get; }

    public double LineWeight { get; }

    public LayerSpec ToLayerSpec()
    {
        return new LayerSpec
        {
            Name = Name,
            Color = Color,
            LineType = LineType,
            LineWeight = LineWeight
        };
    }
}

public static class CadLayerStandards
{
    private static readonly CadLayerStandard[] OrderedStandards =
    {
        new CadLayerStandard(CadLayerNames.Outline, 7, "Continuous", 0.35),
        new CadLayerStandard(CadLayerNames.Center, 1, "Center", 0.18),
        new CadLayerStandard(CadLayerNames.Dimension, 3, "Continuous", 0.18),
        new CadLayerStandard(CadLayerNames.Text, 2, "Continuous", 0.18),
        new CadLayerStandard(CadLayerNames.Hidden, 8, "Hidden", 0.18),
        new CadLayerStandard(CadLayerNames.Construction, 9, "Continuous", 0.09),
        new CadLayerStandard(CadLayerNames.Title, 4, "Continuous", 0.25)
    };

    private static readonly IReadOnlyDictionary<string, CadLayerStandard> StandardByName =
        OrderedStandards.ToDictionary(standard => standard.Name, StringComparer.Ordinal);

    public static IReadOnlyList<CadLayerStandard> All => OrderedStandards;

    public static IReadOnlyDictionary<string, CadLayerStandard> Definitions => StandardByName;

    public static IReadOnlyList<string> RequiredProductionLayerNames { get; } =
        new[] { CadLayerNames.Outline, CadLayerNames.Center, CadLayerNames.Dimension };

    public static bool TryGet(string name, out CadLayerStandard standard)
    {
        return StandardByName.TryGetValue(name, out standard!);
    }
}

public static class CadTextStyleNames
{
    public const string Note = "AI_NOTE_3_5";
    public const string Dimension = "AI_DIM_TEXT_3_5";
    public const string TitlePrimary = "AI_TITLE_5";
    public const string TitleSecondary = "AI_TITLE_3_5";
}

public sealed class CadTextStyleStandard
{
    public CadTextStyleStandard(string name, double height, double widthFactor)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Height = height;
        WidthFactor = widthFactor;
    }

    public string Name { get; }

    public double Height { get; }

    public double WidthFactor { get; }

    public double ObliqueAngle { get; } = 0d;

    public string FontFileName { get; } = "simplex.shx";
}

public static class CadTextStyleStandards
{
    private static readonly IReadOnlyDictionary<string, CadTextStyleStandard> Standards =
        new Dictionary<string, CadTextStyleStandard>(StringComparer.Ordinal)
        {
            [CadTextStyleNames.Note] = new CadTextStyleStandard(CadTextStyleNames.Note, 3.5, 0.8),
            [CadTextStyleNames.Dimension] = new CadTextStyleStandard(CadTextStyleNames.Dimension, 3.5, 0.8),
            [CadTextStyleNames.TitlePrimary] = new CadTextStyleStandard(CadTextStyleNames.TitlePrimary, 5.0, 0.8),
            [CadTextStyleNames.TitleSecondary] = new CadTextStyleStandard(CadTextStyleNames.TitleSecondary, 3.5, 0.8)
        };

    public static IReadOnlyDictionary<string, CadTextStyleStandard> Definitions => Standards;

    public static CadTextStyleStandard ResolveForLayer(string layer, double textHeight)
    {
        if (string.Equals(layer, CadLayerNames.Title, StringComparison.Ordinal))
        {
            return textHeight >= 5d
                ? Standards[CadTextStyleNames.TitlePrimary]
                : Standards[CadTextStyleNames.TitleSecondary];
        }

        return Standards[CadTextStyleNames.Note];
    }
}

public static class CadDimensionStyleNames
{
    public const string Mechanical = "AI_MECH_MM";
    public const string Diameter = "AI_MECH_DIAMETER";
    public const string Radius = "AI_MECH_RADIUS";
}

public sealed class CadDimensionStyleStandard
{
    public CadDimensionStyleStandard(string name, string textStyleName)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TextStyleName = textStyleName ?? throw new ArgumentNullException(nameof(textStyleName));
    }

    public string Name { get; }

    public string TextStyleName { get; }

    public double TextHeight { get; } = 3.5;

    public double ArrowSize { get; } = 3.0;

    public double ExtensionOffset { get; } = 1.0;

    public double ExtensionBeyond { get; } = 1.5;

    public int Precision { get; } = 0;
}

public static class CadDimensionStyleStandards
{
    private static readonly IReadOnlyDictionary<string, CadDimensionStyleStandard> Standards =
        new Dictionary<string, CadDimensionStyleStandard>(StringComparer.Ordinal)
        {
            [CadDimensionStyleNames.Mechanical] =
                new CadDimensionStyleStandard(CadDimensionStyleNames.Mechanical, CadTextStyleNames.Dimension),
            [CadDimensionStyleNames.Diameter] =
                new CadDimensionStyleStandard(CadDimensionStyleNames.Diameter, CadTextStyleNames.Dimension),
            [CadDimensionStyleNames.Radius] =
                new CadDimensionStyleStandard(CadDimensionStyleNames.Radius, CadTextStyleNames.Dimension)
        };

    public static IReadOnlyDictionary<string, CadDimensionStyleStandard> Definitions => Standards;

    public static CadDimensionStyleStandard ResolveForDimensionType(string dimensionType)
    {
        switch (dimensionType)
        {
            case DimensionTypes.Diameter:
                return Standards[CadDimensionStyleNames.Diameter];
            case DimensionTypes.Radius:
                return Standards[CadDimensionStyleNames.Radius];
            default:
                return Standards[CadDimensionStyleNames.Mechanical];
        }
    }
}
