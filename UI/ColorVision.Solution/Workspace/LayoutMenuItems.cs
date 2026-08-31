using ColorVision.Solution.Properties;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Solution.Workspace
{
    /// <summary>
    /// 视图菜单 → 重置窗口布局
    /// </summary>
    public class MenuResetLayout : MenuItemBase, IHotKey
    {
        private readonly Func<bool> _confirmReset;
        private readonly Action _resetLayout;

        public MenuResetLayout() : this(
            () => WorkspaceManager.LayoutManager != null && ConfirmResetLayout(),
            () => WorkspaceManager.LayoutManager?.ResetLayout()) { }

        internal MenuResetLayout(Func<bool> confirmReset, Action resetLayout)
        {
            _confirmReset = confirmReset;
            _resetLayout = resetLayout;
        }

        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => Resources.MenuResetLayout;
        public override int Order => 102;

        public HotKeys HotKeys => new(Resources.MenuResetLayout, new Hotkey(Key.R, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift), Execute)
        {
            Description = BuiltInHotkeyDescriptions.ResetLayout
        };

        public override void Execute()
        {
            if (_confirmReset()) _resetLayout();
        }

        // Only the user-facing action asks: startup recovery still calls the layout
        // manager directly. Default to No because reset may discard unsaved tabs.
        private static bool ConfirmResetLayout() => MessageBox.Show(Application.Current?.GetActiveWindow(),
            BuiltInHotkeyDescriptions.ResetLayoutConfirmation, Resources.MenuResetLayout,
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
    }
}
