using System;
using System.Windows.Forms;
using ZwSoft.ZwCAD.Windows;

namespace ZwcadAi.Plugin;

internal static class AiDrawingPanelHost
{
    private static PaletteSet? _paletteSet;
    private static AiDrawingPanelControl? _panelControl;

    public static void Show()
    {
        if (_paletteSet == null)
        {
            _panelControl = new AiDrawingPanelControl(AiDrawingPluginServices.CreateDefault())
            {
                Dock = DockStyle.Fill
            };
            _paletteSet = new PaletteSet("AI 绘图")
            {
                KeepFocus = true
            };
            _paletteSet.Add("AI 绘图", _panelControl);
        }

        _paletteSet.Visible = true;
    }
}
