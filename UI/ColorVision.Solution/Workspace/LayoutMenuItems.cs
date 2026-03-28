using ColorVision.UI.Menus;
using ColorVision.UI.Views;
using System.Windows;

namespace ColorVision.Solution.Workspace
{
    /// <summary>
    /// 视图菜单 → 保存窗口布局
    /// </summary>
    public class MenuSaveLayout : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => "保存窗口布局";
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
        public override string Header => "应用窗口布局";
        public override int Order => 101;

        public override void Execute()
        {
            if (WorkspaceManager.LayoutManager == null) return;
            if (!WorkspaceManager.LayoutManager.LoadLayout())
            {
                MessageBox.Show("未找到已保存的窗口布局。", "应用窗口布局", MessageBoxButton.OK, MessageBoxImage.Information);
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
        public override string Header => "重置窗口布局";
        public override int Order => 102;

        public override void Execute()
        {
            WorkspaceManager.LayoutManager?.ResetLayout();
        }
    }
}
