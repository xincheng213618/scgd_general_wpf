using ColorVision.UI.Docking;
using System.Windows.Controls;

namespace ColorVision.UI;

/// <summary>Hosts device controls and contributes device-panel commands to the docked pane title.</summary>
public sealed class DisPlayControlPanel : ScrollViewer, IDockPanelTitleActionProvider
{
    private static readonly IReadOnlyList<DockPanelTitleAction> Actions =
    [
        new(DisPlayManager.CreateGroupCommand, "\uE710", "新建分组")
    ];

    public IReadOnlyList<DockPanelTitleAction> TitleActions => Actions;
}
