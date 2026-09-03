using ColorVision.UI.HotKey;
using ColorVision.UI.Menus.Base.File;
using ColorVision.Solution.Workspace;
using AvalonDock;
using ColorVision.Common.MVVM;
using ColorVision.Copilot;
using ColorVision.UI.Serach;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision;

public partial class MainWindow
{
    private SearchWindow? _commandSearchWindow;

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
        // A configured global key must not activate this application from elsewhere.
        if (!IsVisible || (!IsActive && _commandSearchWindow?.IsActive != true)) return;
        if (_commandSearchWindow is { } existing)
        {
            if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
            existing.Activate();
            existing.FocusSearch();
            return;
        }

        IInputElement? target = ResolveSearchCommandTarget(this, Keyboard.FocusedElement);
        var originalDocument = WorkspaceManager.FindDocumentActive(DockingManager1.Layout);
        bool IsCommandContextCurrent() => ReferenceEquals(originalDocument, WorkspaceManager.FindDocumentActive(DockingManager1.Layout));
        var window = new SearchWindow { Owner = this };
        _commandSearchWindow = window;
        var bridge = new SearchWindowHotkeyBridge(this, window, HotkeyService.GetInstance(),
            [typeof(MenuCommandSearch).FullName!, typeof(MenuContextualFind).FullName!], window.FocusSearch);
        window.Closed += (_, _) =>
        {
            bridge.Dispose();
            if (ReferenceEquals(_commandSearchWindow, window)) _commandSearchWindow = null;
            // A non-modal search may outlive switching from document A to B. Never
            // restore A or a generic docking target over the user's newer selection.
            if (IsActive && IsVisible && IsCommandContextCurrent() && IsSearchReturnFocusCurrent(this, target))
                Keyboard.Focus(target);
        };
        window.Show();
        window.Open(target, IsCommandContextCurrent);
    }

    internal void FindInCurrentContext()
    {
        if (_commandSearchWindow?.IsActive == true)
        {
            _commandSearchWindow.FocusSearch();
            return;
        }
        if (!IsActive || !IsVisible) return;

        IInputElement? focused = ResolveSearchCommandTarget(this, Keyboard.FocusedElement);
        if (FindVisualAncestor<CopilotChatPanel>(focused as DependencyObject) is { } chatPanel)
            AttachConversationFindAdapter(chatPanel);
        if (!ContextualFindRouter.TryFind(focused, this)) FocusCommandSearch();
    }

    internal static IInputElement? ResolveSearchCommandTarget(Window owner, IInputElement? keyboardTarget)
    {
        // WPF menus have their own focus scope; opening search from a menu must retain
        // the content's remembered focus, not redirect a document command to the menu.
        if (keyboardTarget is MenuItem || FindVisualAncestor<MenuBase>(keyboardTarget as DependencyObject) != null)
        {
            IInputElement? remembered = FocusManager.GetFocusedElement(owner);
            return remembered is not MenuItem && FindVisualAncestor<MenuBase>(remembered as DependencyObject) == null
                && ContextualFindRouter.IsWithin(remembered, owner) ? remembered : null;
        }
        return ContextualFindRouter.IsWithin(keyboardTarget, owner) ? keyboardTarget : null;
    }

    internal static bool IsSearchReturnFocusCurrent(Window owner, IInputElement? target)
    {
        if (!ReferenceEquals(ResolveSearchCommandTarget(owner, FocusManager.GetFocusedElement(owner)), target)) return false;
        return ContextualFindRouter.IsWithin(target, owner)
            && (target is UIElement { IsVisible: true, IsEnabled: true } or ContentElement { IsEnabled: true });
    }

    internal static void AttachConversationFindAdapter(CopilotChatPanel panel)
    {
        if (ContextualFindRouter.GetLocalFindCommand(panel) != null) return;
        // Adapt the existing local action at the shell boundary; never synthesize Ctrl+F.
        ContextualFindRouter.SetLocalFindCommand(panel, new RelayCommand(_ =>
        {
            if (panel.DataContext is not CopilotChatViewModel viewModel || !viewModel.OpenConversationFindCommand.CanExecute(null)) return;
            viewModel.OpenConversationFindCommand.Execute(null);
            panel.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (panel.IsLoaded && panel.FindName("ConversationFindTextBox") is TextBox { IsVisible: true } searchBox)
                {
                    searchBox.Focus();
                    searchBox.SelectAll();
                }
            });
        }, _ => panel.DataContext is CopilotChatViewModel viewModel && viewModel.OpenConversationFindCommand.CanExecute(null)));
    }

}

// Keep the provider type names stable so saved hotkey overrides retain their IDs.
public sealed class MenuCommandSearch : IHotKey
{
    public HotKeys HotKeys => new(BuiltInHotkeyDescriptions.SearchCommandsName, new Hotkey(Key.P, ModifierKeys.Control | ModifierKeys.Shift), Execute)
    {
        Description = BuiltInHotkeyDescriptions.SearchCommands,
        Category = ColorVision.UI.Properties.Resources.MenuTool
    };

    public void Execute()
    {
        if (Application.Current?.MainWindow is MainWindow window) window.FocusCommandSearch();
    }
}

public sealed class MenuContextualFind : IHotKey
{
    public HotKeys HotKeys => new(BuiltInHotkeyDescriptions.ContextualFindName, new Hotkey(Key.F, ModifierKeys.Control), Execute)
    {
        Description = BuiltInHotkeyDescriptions.ContextualFind,
        Category = ColorVision.UI.Properties.Resources.MenuEdit
    };

    public void Execute()
    {
        if (Application.Current?.MainWindow is MainWindow window) window.FindInCurrentContext();
    }
}
