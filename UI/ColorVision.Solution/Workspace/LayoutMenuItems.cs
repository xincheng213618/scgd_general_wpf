using ColorVision.Solution.Properties;
using ColorVision.UI.Menus;

namespace ColorVision.Solution.Workspace
{
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
