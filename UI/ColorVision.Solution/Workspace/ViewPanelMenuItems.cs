using ColorVision.Common.MVVM;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Solution.Workspace
{
    /// <summary>
    /// 动态把 Dock 面板暴露到“视图”菜单，便于用户按需打开/隐藏工具窗口。
    /// </summary>
    public class ViewPanelMenuItemProvider : IMenuItemProvider
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            var layoutManager = WorkspaceManager.LayoutManager;
            if (layoutManager == null)
                return Enumerable.Empty<MenuItemMetadata>();

            return layoutManager.GetRegisteredPanels().Select(CreateMenuItem);
        }

        private static MenuItemMetadata CreateMenuItem(RegisteredPanelInfo panel, int index)
        {
            string contentId = panel.ContentId;

            return new MenuItemMetadata
            {
                TargetName = MenuItemConstants.MainWindowTarget,
                OwnerGuid = MenuItemConstants.View,
                GuidId = $"DockPanel_{contentId}",
                Header = panel.Title,
                Order = GetOrder(panel.Position, index),
                Visibility = Visibility.Visible,
                Command = new RelayCommand(_ =>
                {
                    WorkspaceManager.LayoutManager?.ShowPanel(contentId);
                })
            };
        }

        private static int GetOrder(PanelPosition position, int index)
        {
            if (position == PanelPosition.Bottom)
                return 60 + index;

            int baseOrder = position switch
            {
                PanelPosition.Left => 10,
                PanelPosition.Right => 40,
                _ => 80
            };

            return baseOrder + index;
        }
    }
}
