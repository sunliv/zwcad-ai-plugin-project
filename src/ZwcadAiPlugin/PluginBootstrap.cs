using System;
using System.Diagnostics;
using ZwcadAi.Core;
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
        CadLog.WriteInfo("ZWCAD AI plugin loaded. Command AIDRAW is registered.");
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

            CadLog.WriteInfo(
                $"{status.Product}: {status.Command} is ready for {status.Domain}. No DWG changes were made.");
        }
        catch (System.Exception exception)
        {
            CadLog.WriteError($"AIDRAW failed: {exception.Message}", exception);
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
