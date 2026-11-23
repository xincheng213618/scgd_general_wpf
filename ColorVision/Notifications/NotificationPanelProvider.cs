using ColorVision.Common.MVVM;
using ColorVision.Solution.Workspace;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Notifications;

public sealed class NotificationPanelProvider : IDockPanelProvider, IStatusBarProvider
{
    private const string PanelId = "NotificationPanel";

    public int Order => 220;

    public void RegisterPanels()
    {
        WorkspaceManager.LayoutManager?.RegisterPanel(
            PanelId, () => new NotificationPanel(), NotificationText.Title, PanelPosition.Right, isDefaultVisible: false);
    }

    public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
    {
        yield return new StatusBarMeta
        {
            Id = PanelId,
            Name = NotificationText.Title,
            Alignment = StatusBarAlignment.Right,
            Order = int.MaxValue,
            TargetName = MenuItemConstants.MainWindowTarget,
            ContentFactory = CreateBellButton,
        };
    }

    private static Button CreateBellButton()
    {
        var button = new Button
        {
            Content = "\uEA8F", // Segoe Ringer glyph
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 16,
            Width = 28,
            Height = 28,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Focusable = true,
            ToolTip = NotificationText.Title,
            Command = new RelayCommand(_ => WorkspaceManager.LayoutManager?.ShowPanel(PanelId)),
        };
        button.SetResourceReference(Button.StyleProperty, "PanelIconButtonStyle");
        button.SetResourceReference(Button.ForegroundProperty, "GlobalTextBrush");
        AutomationProperties.SetName(button, NotificationText.Title);
        return button;
    }
}
