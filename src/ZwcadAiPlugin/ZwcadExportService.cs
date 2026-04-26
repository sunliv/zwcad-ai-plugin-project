using System;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.PlottingServices;
using Application = ZwSoft.ZwCAD.ApplicationServices.Application;
using PlotType = ZwSoft.ZwCAD.DatabaseServices.PlotType;

namespace ZwcadAi.Plugin;

public sealed class ZwcadExportService
{
    public ExportResult ExportActiveDocument()
    {
        var document = Application.DocumentManager?.MdiActiveDocument
            ?? throw new InvalidOperationException("No active ZWCAD document is available.");
        var database = document.Database
            ?? throw new InvalidOperationException("No active ZWCAD database is available.");

        var exportPaths = ExportPaths.Create(document.Name, DateTime.Now);
        Directory.CreateDirectory(exportPaths.Directory);

        SaveDwgCopy(document, database, exportPaths.DwgPath);
        var pdfStatus = ExportPdf(document, database, exportPaths.PdfPath);

        return new ExportResult(
            exportPaths.DwgPath,
            pdfStatus.Path,
            pdfStatus.Succeeded,
            pdfStatus.Message);
    }

    private static void SaveDwgCopy(Document document, Database database, string dwgPath)
    {
        using (document.LockDocument())
        using (var copy = database.Wblock())
        {
            copy.SaveAs(dwgPath, DwgVersion.Current);
        }
    }

    private static PdfExportStatus ExportPdf(Document document, Database database, string pdfPath)
    {
        object? previousBackgroundPlot = null;
        var shouldRestoreBackgroundPlot = false;

        try
        {
            if (PlotFactory.ProcessPlotState != ProcessPlotState.NotPlotting)
            {
                return PdfExportStatus.Unavailable(pdfPath, "A ZWCAD plot job is already in progress.");
            }

            previousBackgroundPlot = Application.GetSystemVariable("BACKGROUNDPLOT");
            shouldRestoreBackgroundPlot = true;
            Application.SetSystemVariable("BACKGROUNDPLOT", 0);

            using (document.LockDocument())
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var layoutManager = LayoutManager.Current;
                var layoutId = layoutManager.GetLayoutId(layoutManager.CurrentLayout);
                var layout = (Layout)transaction.GetObject(layoutId, OpenMode.ForRead);

                using (var plotSettings = new PlotSettings(layout.ModelType))
                {
                    plotSettings.CopyFrom(layout);

                    var settingsValidator = PlotSettingsValidator.Current;
                    var pdfDevice = SelectPdfDevice(settingsValidator.GetPlotDeviceList());
                    if (pdfDevice == null)
                    {
                        return PdfExportStatus.Unavailable(pdfPath, "No ZWCAD PDF plot device is available.");
                    }

                    settingsValidator.SetPlotConfigurationName(plotSettings, pdfDevice, null);
                    settingsValidator.RefreshLists(plotSettings);
                    SetMediaName(settingsValidator, plotSettings);
                    settingsValidator.SetPlotType(plotSettings, PlotType.Extents);
                    settingsValidator.SetUseStandardScale(plotSettings, true);
                    settingsValidator.SetStdScaleType(plotSettings, StdScaleType.ScaleToFit);
                    settingsValidator.SetPlotCentered(plotSettings, true);
                    TrySetPlotStyleSheet(settingsValidator, plotSettings);

                    var plotConfig = PlotConfigManager.SetCurrentConfig(pdfDevice);
                    if (plotConfig.PlotToFileCapability == PlotToFileCapability.NoPlotToFile)
                    {
                        return PdfExportStatus.Unavailable(
                            pdfPath,
                            $"ZWCAD plot device '{pdfDevice}' cannot plot to a file.");
                    }

                    var plotInfo = new PlotInfo
                    {
                        Layout = layout.ObjectId,
                        OverrideSettings = plotSettings
                    };
                    var infoValidator = new PlotInfoValidator
                    {
                        MediaMatchingPolicy = MatchingPolicy.MatchEnabled
                    };
                    infoValidator.Validate(plotInfo);

                    using (var plotEngine = PlotFactory.CreatePublishEngine())
                    {
                        plotEngine.BeginPlot(null, null);
                        plotEngine.BeginDocument(plotInfo, document.Name, null, 1, true, pdfPath);

                        var pageInfo = new PlotPageInfo();
                        plotEngine.BeginPage(pageInfo, plotInfo, true, null);
                        plotEngine.BeginGenerateGraphics(null);
                        plotEngine.EndGenerateGraphics(null);
                        plotEngine.EndPage(null);
                        plotEngine.EndDocument(null);
                        plotEngine.EndPlot(null);
                    }
                }

                transaction.Commit();
            }

            return File.Exists(pdfPath)
                ? PdfExportStatus.Success(pdfPath)
                : PdfExportStatus.Unavailable(
                    pdfPath,
                    "ZWCAD plot engine completed, but no PDF file was created at the expected path.");
        }
        catch (Exception exception)
        {
            return PdfExportStatus.Unavailable(pdfPath, exception.Message);
        }
        finally
        {
            if (shouldRestoreBackgroundPlot)
            {
                RestoreBackgroundPlot(previousBackgroundPlot);
            }
        }
    }

    private static string? SelectPdfDevice(StringCollection devices)
    {
        var preferredNames = new[]
        {
            "DWG To PDF.pc5",
            "ZWCAD PDF.pc5",
            "ZwPDFDriver"
        };

        foreach (var preferredName in preferredNames)
        {
            var match = devices
                .Cast<string>()
                .FirstOrDefault(device => string.Equals(device, preferredName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match;
            }
        }

        return devices
            .Cast<string>()
            .FirstOrDefault(device => device.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static void SetMediaName(PlotSettingsValidator settingsValidator, PlotSettings plotSettings)
    {
        var mediaNames = settingsValidator.GetCanonicalMediaNameList(plotSettings);
        if (mediaNames.Count == 0)
        {
            return;
        }

        var currentMediaName = plotSettings.CanonicalMediaName;
        if (!string.IsNullOrWhiteSpace(currentMediaName)
            && mediaNames.Cast<string>().Any(mediaName => string.Equals(mediaName, currentMediaName, StringComparison.Ordinal)))
        {
            return;
        }

        settingsValidator.SetCanonicalMediaName(plotSettings, mediaNames[0]);
    }

    private static void TrySetPlotStyleSheet(PlotSettingsValidator settingsValidator, PlotSettings plotSettings)
    {
        var styleSheets = settingsValidator.GetPlotStyleSheetList();
        var styleSheet = styleSheets
            .Cast<string>()
            .FirstOrDefault(sheet => string.Equals(sheet, "monochrome.ctb", StringComparison.OrdinalIgnoreCase))
            ?? styleSheets.Cast<string>().FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(styleSheet))
        {
            settingsValidator.SetCurrentStyleSheet(plotSettings, styleSheet);
        }
    }

    private static void RestoreBackgroundPlot(object? previousBackgroundPlot)
    {
        try
        {
            if (previousBackgroundPlot != null)
            {
                Application.SetSystemVariable("BACKGROUNDPLOT", previousBackgroundPlot);
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError(exception.ToString());
        }
    }

    private sealed class ExportPaths
    {
        private ExportPaths(string directory, string dwgPath, string pdfPath)
        {
            Directory = directory;
            DwgPath = dwgPath;
            PdfPath = pdfPath;
        }

        public string Directory { get; }

        public string DwgPath { get; }

        public string PdfPath { get; }

        public static ExportPaths Create(string documentName, DateTime timestamp)
        {
            var baseDirectory = ResolveBaseDirectory(documentName);
            var exportDirectory = Path.Combine(baseDirectory, "ZwcadAiExports");
            var fileStem = ResolveFileStem(documentName);
            var suffix = timestamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var outputStem = $"{fileStem}-aiexport-{suffix}";

            return new ExportPaths(
                exportDirectory,
                Path.Combine(exportDirectory, outputStem + ".dwg"),
                Path.Combine(exportDirectory, outputStem + ".pdf"));
        }

        private static string ResolveBaseDirectory(string documentName)
        {
            try
            {
                var directory = Path.GetDirectoryName(documentName);
                if (!string.IsNullOrWhiteSpace(directory) && System.IO.Directory.Exists(directory))
                {
                    return directory;
                }
            }
            catch (ArgumentException)
            {
            }

            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return string.IsNullOrWhiteSpace(documents) ? Path.GetTempPath() : documents;
        }

        private static string ResolveFileStem(string documentName)
        {
            string fileNameWithoutExtension;
            try
            {
                fileNameWithoutExtension = Path.GetFileNameWithoutExtension(documentName);
            }
            catch (ArgumentException)
            {
                fileNameWithoutExtension = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            {
                fileNameWithoutExtension = "zwcad-ai-export";
            }

            var invalidCharacters = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileNameWithoutExtension
                .Select(character => invalidCharacters.Contains(character) ? '_' : character)
                .ToArray());

            return string.IsNullOrWhiteSpace(sanitized) ? "zwcad-ai-export" : sanitized;
        }
    }
}

public sealed class ExportResult
{
    public ExportResult(string dwgPath, string pdfPath, bool pdfSucceeded, string pdfMessage)
    {
        DwgPath = dwgPath ?? throw new ArgumentNullException(nameof(dwgPath));
        PdfPath = pdfPath ?? throw new ArgumentNullException(nameof(pdfPath));
        PdfSucceeded = pdfSucceeded;
        PdfMessage = pdfMessage ?? throw new ArgumentNullException(nameof(pdfMessage));
    }

    public string DwgPath { get; }

    public string PdfPath { get; }

    public bool PdfSucceeded { get; }

    public string PdfMessage { get; }
}

internal sealed class PdfExportStatus
{
    private PdfExportStatus(string path, bool succeeded, string message)
    {
        Path = path;
        Succeeded = succeeded;
        Message = message;
    }

    public string Path { get; }

    public bool Succeeded { get; }

    public string Message { get; }

    public static PdfExportStatus Success(string path)
    {
        return new PdfExportStatus(path, true, "PDF exported.");
    }

    public static PdfExportStatus Unavailable(string path, string reason)
    {
        return new PdfExportStatus(path, false, reason);
    }
}
