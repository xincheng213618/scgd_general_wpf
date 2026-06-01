using ColorVision.Solution.Properties;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Solution.Workspace
{
    /// <summary>
    /// 视图菜单 → 保存窗口布局
    /// </summary>
    public class MenuSaveLayout : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => Resources.MenuSaveLayout;
        public override int Order => 100;

        public override void Execute()
        {
            WorkspaceManager.LayoutManager?.SaveLayout();
        }
    }

    /// <summary>
    /// 视图菜单 → 应用窗口布局
    /// </summary>
    public class MenuApplyLayout : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => Resources.MenuApplyLayout;
        public override int Order => 101;

        public override void Execute()
        {
            if (WorkspaceManager.LayoutManager == null) return;
            if (!WorkspaceManager.LayoutManager.LoadLayout())
            {
                MessageBox.Show(Resources.LayoutNotFoundMessage, Resources.LayoutNotFoundTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // 布局加载后重新创建所有视图标签页
                UI.Views.DockViewManager.GetInstance().ShowAllViews();
            }
        }
    }

    /// <summary>
    /// 视图菜单 → 重置窗口布局
    /// </summary>
    public class MenuResetLayout : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => Resources.MenuResetLayout;
        public override int Order => 102;

        public override void Execute()
        {
            WorkspaceManager.LayoutManager?.ResetLayout();
        }
    }
}
