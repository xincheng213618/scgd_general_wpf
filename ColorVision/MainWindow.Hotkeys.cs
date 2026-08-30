using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using ColorVision.UI.Menus.Base.File;
using ColorVision.Solution.Workspace;
using AvalonDock;
using System.Windows;
using System.Windows.Input;

namespace ColorVision;

public partial class MainWindow
{
    internal static CommandBinding CreateCloseDocumentBinding(DockingManager manager)
    {
        // Layout restore/reset replaces the root; never capture the initial XAML tree.
        // This command closes a tab, not ApplicationCommands.Close's editor-specific Clear.
        return new CommandBinding(MenuClose.CloseDocumentCommand,
            (_, e) => { WorkspaceManager.FindDocumentActive(manager.Layout)?.Close(); e.Handled = true; },
            (_, e) => { e.CanExecute = WorkspaceManager.FindDocumentActive(manager.Layout)?.CanClose == true; e.Handled = true; });
    }

    internal void FocusCommandSearch()
    {
        if (ActualWidth < 700)
        {
            CompactSearchControl.Visibility = Visibility.Visible;
            CompactSearchControl.FocusSearchBox();
        }
        else
        {
            SearchControl1.Visibility = Visibility.Visible;
            SearchControl1.FocusSearchBox();
        }
    }

    private void CompactSearchControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || Keyboard.Modifiers != ModifierKeys.None) return;
        CompactSearchControl.Visibility = Visibility.Collapsed;
        DockingManager1.Focus();
        e.Handled = true;
    }
}

public sealed class MenuCommandSearch : MenuItemBase, IHotKey
{
    public override string OwnerGuid => MenuItemConstants.Tool;
    public override string GuidId => nameof(MenuCommandSearch);
    public override string Header => BuiltInHotkeyDescriptions.SearchCommandsName;
    public override int Order => 10;
    public HotKeys HotKeys => new(Header, new Hotkey(Key.P, ModifierKeys.Control | ModifierKeys.Shift), Execute)
    {
        Description = BuiltInHotkeyDescriptions.SearchCommands
    };

    public override void Execute()
    {
        if (Application.Current?.MainWindow is MainWindow window) window.FocusCommandSearch();
    }
}
