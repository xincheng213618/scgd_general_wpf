using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using ColorVision.UI.Menus.Base.File;
using ColorVision.Solution.Workspace;
using AvalonDock;
using ColorVision.Common.MVVM;
using ColorVision.Copilot;
using ColorVision.UI.Serach;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision;

public partial class MainWindow
{
    private IInputElement? _commandSearchReturnFocus;
    private HotKeys? _commandSearchHotkey;

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
        // A configured global key must not leave a topmost popup over another application.
        if (!IsActive || !IsVisible) return;
        if (!CommandSearchPopup.IsOpen)
        {
            _commandSearchReturnFocus = ResolveSearchCommandTarget(this, Keyboard.FocusedElement);
            CommandSearchControl.Width = Math.Max(0, Math.Min(720, Root.ActualWidth - 32));
            CommandSearchControl.MaxHeight = Math.Max(0, Root.ActualHeight - 64);
            CommandSearchPopup.IsOpen = true;
        }
        CommandSearchControl.Open(_commandSearchReturnFocus);
    }

    internal void FindInCurrentContext()
    {
        if (!IsActive || !IsVisible) return;
        if (CommandSearchPopup.IsOpen)
        {
            CommandSearchControl.FocusSearchBox();
            return;
        }

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

    private void SearchEntryButton_Click(object sender, RoutedEventArgs e) => FocusCommandSearch();

    private void SearchEntryButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Keep the document focus as the command target before Button takes keyboard focus.
        FocusCommandSearch();
        e.Handled = true;
    }

    internal static CustomPopupPlacement[] PlaceCommandSearch(Size popupSize, Size targetSize, Point offset)
        => [new CustomPopupPlacement(new Point(Math.Max(16, (targetSize.Width - popupSize.Width) / 2), 48), PopupPrimaryAxis.None)];

    private void CommandSearchPopup_Opened(object? sender, EventArgs e) => CommandSearchControl.FocusSearchBox();
    private void CommandSearchPopup_Closed(object? sender, EventArgs e) => CommandSearchControl.Close();
    private void DismissCommandSearch(object? sender, EventArgs e)
    {
        CommandSearchControl.Close();
        CommandSearchPopup.IsOpen = false;
    }

    private void CommandSearchControl_Closed(object? sender, EventArgs e)
    {
        CommandSearchPopup.IsOpen = false;
        IInputElement? target = _commandSearchReturnFocus;
        _commandSearchReturnFocus = null;
        // Closing after Alt+Tab or host shutdown must never reactivate the owner.
        if (!IsActive || !IsVisible) return;
        if (ContextualFindRouter.IsWithin(target, this) && target is UIElement { IsVisible: true, IsEnabled: true })
            Keyboard.Focus(target);
        else if (ContextualFindRouter.IsWithin(target, this) && target is ContentElement { IsEnabled: true })
            Keyboard.Focus(target);
        else
            DockingManager1.Focus();
    }

    private void InitializeCommandSearch()
    {
        var hotkeys = HotkeyService.GetInstance().HotKeys;
        hotkeys.CollectionChanged += CommandSearchHotkeys_CollectionChanged;
        CommandSearchPopup.CustomPopupPlacementCallback = PlaceCommandSearch;
        var popupHotkeys = new SearchPopupHotkeyBridge(this, CommandSearchControl, HotkeyService.GetInstance(),
            [typeof(MenuCommandSearch).FullName!, typeof(MenuContextualFind).FullName!], CommandSearchControl.FocusSearchBox);
        LocationChanged += DismissCommandSearch;
        SizeChanged += DismissCommandSearch;
        StateChanged += DismissCommandSearch;
        Deactivated += DismissCommandSearch;
        RefreshCommandSearchGesture();
        Closed += (_, _) =>
        {
            hotkeys.CollectionChanged -= CommandSearchHotkeys_CollectionChanged;
            if (_commandSearchHotkey != null) _commandSearchHotkey.PropertyChanged -= CommandSearchHotkey_PropertyChanged;
            popupHotkeys.Dispose();
            LocationChanged -= DismissCommandSearch;
            SizeChanged -= DismissCommandSearch;
            StateChanged -= DismissCommandSearch;
            Deactivated -= DismissCommandSearch;
            DismissCommandSearch(this, EventArgs.Empty);
        };
    }

    private void CommandSearchHotkeys_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshCommandSearchGesture();
    private void CommandSearchHotkey_PropertyChanged(object? sender, PropertyChangedEventArgs e) => RefreshCommandSearchGesture();

    private void RefreshCommandSearchGesture()
    {
        HotKeys? current = HotkeyService.GetInstance().HotKeys.FirstOrDefault(action => action.Id == typeof(MenuCommandSearch).FullName);
        if (!ReferenceEquals(current, _commandSearchHotkey))
        {
            if (_commandSearchHotkey != null) _commandSearchHotkey.PropertyChanged -= CommandSearchHotkey_PropertyChanged;
            _commandSearchHotkey = current;
            if (_commandSearchHotkey != null) _commandSearchHotkey.PropertyChanged += CommandSearchHotkey_PropertyChanged;
        }
        string gesture = current == null ? string.Empty : string.Join(" / ", current.GetBindings().Select(HotkeyInput.Format));
        SearchEntryGestureText.Text = gesture;
        SearchEntryButton.ToolTip = string.IsNullOrWhiteSpace(gesture) ? BuiltInHotkeyDescriptions.SearchCommandsName : $"{BuiltInHotkeyDescriptions.SearchCommandsName} ({gesture})";
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

public sealed class MenuContextualFind : MenuItemBase, IHotKey
{
    public override string OwnerGuid => MenuItemConstants.Edit;
    public override string GuidId => nameof(MenuContextualFind);
    public override string Header => BuiltInHotkeyDescriptions.ContextualFindName;
    public override int Order => 20;
    public HotKeys HotKeys => new(Header, new Hotkey(Key.F, ModifierKeys.Control), Execute)
    {
        Description = BuiltInHotkeyDescriptions.ContextualFind
    };

    public override void Execute()
    {
        if (Application.Current?.MainWindow is MainWindow window) window.FindInCurrentContext();
    }
}
