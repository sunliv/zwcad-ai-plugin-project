using System;
using System.Collections.Generic;
using System.Linq;
using ZwcadAi.AiService;
using ZwcadAi.Core;
using ZwcadAi.Renderer;

namespace ZwcadAi.Ui;

public sealed class AiDrawingPanelState
{
    public AiDrawingSpecResponseKind ResponseKind { get; set; } = AiDrawingSpecResponseKind.Unknown;

    public IReadOnlyList<string> ClarificationQuestions { get; set; } = Array.Empty<string>();

    public IReadOnlyList<AiDrawingPanelIssue> Issues { get; set; } = Array.Empty<AiDrawingPanelIssue>();

    public AiDrawingPreviewSummary? Preview { get; set; }

    public bool ConfirmEnabled { get; set; }
}

public sealed class AiDrawingPanelIssue
{
    public string Code { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public bool Repairable { get; set; }
}

public sealed class AiDrawingPreviewSummary
{
    public RenderStatus Status { get; set; } = RenderStatus.Failed;

    public int EntityCount { get; set; }

    public int DimensionCount { get; set; }

    public int CadObjectCount { get; set; }

    public IReadOnlyDictionary<string, int> TypeCounts { get; set; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> LayerCounts { get; set; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public AiDrawingBoundsSummary? Bounds { get; set; }

    public IReadOnlyDictionary<string, string> SpecIdMappings { get; set; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public string OutputPath { get; set; } = string.Empty;

    public string ExportStatus { get; set; } = string.Empty;
}

public sealed class AiDrawingBoundsSummary
{
    public double MinX { get; set; }

    public double MinY { get; set; }

    public double MaxX { get; set; }

    public double MaxY { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }
}

public static class AiDrawingPanelStateMapper
{
    public static AiDrawingPanelState FromResponse(
        AiDrawingSpecResponse response,
        RenderResult? previewRenderResult = null)
    {
        if (response == null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        var preview = previewRenderResult == null
            ? null
            : MapPreview(previewRenderResult.Summary);
        var issues = MapIssues(response, previewRenderResult).ToArray();

        return new AiDrawingPanelState
        {
            ResponseKind = response.Kind,
            ClarificationQuestions = NormalizeStrings(response.Clarifications).ToArray(),
            Issues = issues,
            Preview = preview,
            ConfirmEnabled = CanConfirm(response, previewRenderResult)
        };
    }

    private static bool CanConfirm(AiDrawingSpecResponse response, RenderResult? previewRenderResult)
    {
        return response.Kind == AiDrawingSpecResponseKind.DrawingSpec
            && response.Spec != null
            && response.Validation.IsValid
            && previewRenderResult != null
            && previewRenderResult.Success
            && previewRenderResult.Validation.IsValid
            && previewRenderResult.Summary.Success;
    }

    private static IEnumerable<AiDrawingPanelIssue> MapIssues(
        AiDrawingSpecResponse response,
        RenderResult? previewRenderResult)
    {
        foreach (var issue in response.Issues ?? Array.Empty<AiModelIssue>())
        {
            yield return new AiDrawingPanelIssue
            {
                Code = issue.Code ?? string.Empty,
                Path = issue.Path ?? string.Empty,
                Message = issue.Message ?? string.Empty,
                Severity = issue.Severity.ToString(),
                Source = issue.Source.ToString(),
                Repairable = issue.Repairable
            };
        }

        if (response.Issues == null || response.Issues.Count == 0)
        {
            foreach (var issue in response.Validation.Issues)
            {
                yield return MapValidationIssue(issue, "Validation", repairable: false);
            }
        }

        if (previewRenderResult != null && !previewRenderResult.Validation.IsValid)
        {
            foreach (var issue in previewRenderResult.Validation.Issues)
            {
                yield return MapValidationIssue(issue, "Renderer", repairable: false);
            }
        }
    }

    private static AiDrawingPanelIssue MapValidationIssue(
        ValidationIssue issue,
        string source,
        bool repairable)
    {
        return new AiDrawingPanelIssue
        {
            Code = issue.Code,
            Path = issue.Path,
            Message = issue.Message,
            Severity = issue.Severity.ToString(),
            Source = source,
            Repairable = repairable
        };
    }

    private static AiDrawingPreviewSummary MapPreview(GeometrySummary summary)
    {
        return new AiDrawingPreviewSummary
        {
            Status = summary.Status,
            EntityCount = summary.EntityCount,
            DimensionCount = summary.DimensionCount,
            CadObjectCount = summary.CadObjectCount,
            TypeCounts = ToDictionary(summary.TypeCounts),
            LayerCounts = ToDictionary(summary.LayerCounts),
            Bounds = MapBounds(summary.Bounds),
            SpecIdMappings = ToDictionary(summary.ObjectIdBySpecId),
            OutputPath = summary.OutputPath,
            ExportStatus = summary.ExportStatus
        };
    }

    private static IReadOnlyDictionary<string, int> ToDictionary(IReadOnlyDictionary<string, int> values)
    {
        return values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> ToDictionary(IReadOnlyDictionary<string, string> values)
    {
        return values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    private static AiDrawingBoundsSummary? MapBounds(GeometryBounds? bounds)
    {
        if (bounds == null)
        {
            return null;
        }

        return new AiDrawingBoundsSummary
        {
            MinX = bounds.MinX,
            MinY = bounds.MinY,
            MaxX = bounds.MaxX,
            MaxY = bounds.MaxY,
            Width = bounds.Width,
            Height = bounds.Height
        };
    }

    private static IEnumerable<string> NormalizeStrings(IReadOnlyList<string>? values)
    {
        return (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim());
    }
}
