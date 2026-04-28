using System;
using System.Diagnostics;
using System.Linq;
using ZwcadAi.Core;
using ZwcadAi.Renderer;
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.Runtime;

[assembly: ExtensionApplication(typeof(ZwcadAi.Plugin.PluginApplication))]
[assembly: CommandClass(typeof(ZwcadAi.Plugin.PluginCommands))]

namespace ZwcadAi.Plugin;

public sealed class PluginBootstrap
{
    public PluginRuntimeStatus GetStatus()
    {
        return new PluginRuntimeStatus(
            product: "ZWCAD AI Drawing Plugin",
            domain: DrawingDomain.MechanicalPlate,
            command: PluginCommandCatalog.AiDraw,
            readyForCadLoad: true);
    }
}

public sealed class PluginApplication : IExtensionApplication
{
    public void Initialize()
    {
        CadLog.WriteInfo("ZWCAD AI plugin loaded. Commands AIDRAW and AIEXPORT are registered.");
    }

    public void Terminate()
    {
        CadLog.WriteInfo("ZWCAD AI plugin unloaded.");
    }
}

public sealed class PluginCommands
{
    [CommandMethod(PluginCommandCatalog.AiDraw)]
    public void AiDraw()
    {
        try
        {
            var status = new PluginBootstrap().GetStatus();
            var spec = RectangularPlateSample.Create();
            var renderer = new DrawingSpecPlanRenderer();
            var plan = renderer.CreatePlan(spec);

            if (!plan.Validation.IsValid)
            {
                var errors = string.Join(
                    "; ",
                    plan.Validation.Issues.Select(issue => $"{issue.Code} at {issue.Path}: {issue.Message}"));
                CadLog.WriteInfo($"{status.Command} POC sample validation failed: {errors}");
                return;
            }

            var renderResult = new ZwcadDrawingWriter().Render(plan);
            if (renderResult.Canceled)
            {
                CadLog.WriteInfo($"{status.Command} render canceled before committing CAD entities.");
                return;
            }

            if (!renderResult.Success)
            {
                var errors = string.Join(
                    "; ",
                    renderResult.Validation.Issues.Select(issue => $"{issue.Code} at {issue.Path}: {issue.Message}"));
                CadLog.WriteInfo($"{status.Command} render failed and was rolled back: {errors}");
                return;
            }

            CadLog.WriteInfo(
                $"{status.Product}: {status.Command} rendered fixed {status.Domain} POC sample "
                + $"'{spec.Metadata.RequestId}' with {renderResult.Entities.Count} CAD entities.");
        }
        catch (System.Exception exception)
        {
            CadLog.WriteError($"AIDRAW failed: {exception.Message}", exception);
        }
    }

    [CommandMethod(PluginCommandCatalog.AiExport)]
    public void AiExport()
    {
        try
        {
            var result = new ZwcadExportService().ExportActiveDocument();

            CadLog.WriteInfo($"AIEXPORT DWG copy: {result.DwgPath}");
            if (result.PdfSucceeded)
            {
                CadLog.WriteInfo($"AIEXPORT PDF: {result.PdfPath}");
                return;
            }

            CadLog.WriteInfo($"AIEXPORT PDF export unavailable: {result.PdfMessage}");
        }
        catch (System.Exception exception)
        {
            CadLog.WriteError($"AIEXPORT failed without saving over the active drawing: {exception.Message}", exception);
        }
    }
}

public static class PluginCommandCatalog
{
    public const string AiDraw = "AIDRAW";
    public const string AiCheck = "AICHECK";
    public const string AiExport = "AIEXPORT";
    public const string AiSettings = "AISETTINGS";
}

public sealed class PluginRuntimeStatus
{
    public PluginRuntimeStatus(string product, string domain, string command, bool readyForCadLoad)
    {
        Product = product ?? throw new ArgumentNullException(nameof(product));
        Domain = domain ?? throw new ArgumentNullException(nameof(domain));
        Command = command ?? throw new ArgumentNullException(nameof(command));
        ReadyForCadLoad = readyForCadLoad;
    }

    public string Product { get; }

    public string Domain { get; }

    public string Command { get; }

    public bool ReadyForCadLoad { get; }
}

internal static class CadLog
{
    public static void WriteInfo(string message)
    {
        WriteMessage(message);
        Trace.TraceInformation(message);
    }

    public static void WriteError(string message, System.Exception exception)
    {
        WriteMessage(message);
        Trace.TraceError(exception.ToString());
    }

    private static void WriteMessage(string message)
    {
        try
        {
            var document = Application.DocumentManager?.MdiActiveDocument;
            var editor = document?.Editor;

            if (editor == null)
            {
                Trace.WriteLine(message);
                return;
            }

            editor.WriteMessage($"\n{message}");
        }
        catch (System.Exception exception)
        {
            Trace.TraceError(exception.ToString());
        }
    }
}
