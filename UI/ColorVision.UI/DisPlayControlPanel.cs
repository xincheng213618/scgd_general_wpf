using ColorVision.UI.Docking;
using System.Windows.Controls;

namespace ColorVision.UI;

/// <summary>Hosts device controls and contributes device-panel commands to the docked pane title.</summary>
public sealed class DisPlayControlPanel : ScrollViewer, IDockPanelTitleActionProvider
{
    private static readonly IReadOnlyList<DockPanelTitleAction> Actions =
    [
        new(DisPlayManager.ConfigureDevicesCommand, "\uE710", "添加/配置设备"),
        new(DisPlayManager.ManageControlsCommand, "\uE713", "管理设备控制")
    ];

    public DisPlayControlPanel()
    {
        // An implicit ScrollViewer style is keyed by the exact base type. Keep
        // the existing themed scrollbar after introducing this derived host.
        SetResourceReference(StyleProperty, typeof(ScrollViewer));
    }

    public IReadOnlyList<DockPanelTitleAction> TitleActions => Actions;
}
