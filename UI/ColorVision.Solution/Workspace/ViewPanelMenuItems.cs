using ColorVision.Common.MVVM;
using ColorVision.UI.Menus;
using System;
using System.Windows;

namespace ColorVision.Solution.Workspace
{
    /// <summary>
    /// 动态把 Dock 面板暴露到“视图”菜单，便于用户按需打开/隐藏工具窗口。
    /// </summary>
    public class ViewPanelMenuItemProvider : IMenuItemProvider
    {
        private const string MorePanelsGuid = "DockPanel_More";
        private const string ServiceLogPanelPrefix = "ServiceLog_";

        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            var layoutManager = WorkspaceManager.LayoutManager;
            if (layoutManager == null)
                return Enumerable.Empty<MenuItemMetadata>();

            var panels = layoutManager.GetRegisteredPanels();
            var items = panels
                .Select((panel, index) => CreateMenuItem(panel, index))
                .ToList();

            if (panels.Any(IsMorePanel))
            {
                items.Add(new MenuItemMetadata
                {
                    TargetName = MenuItemConstants.MainWindowTarget,
                    OwnerGuid = MenuItemConstants.View,
                    GuidId = MorePanelsGuid,
                    Header = "更多",
                    Order = 90,
                    Visibility = Visibility.Visible,
                    Command = new RelayCommand(_ => { })
                });
            }

            return items;
        }

        private static MenuItemMetadata CreateMenuItem(RegisteredPanelInfo panel, int index)
        {
            string contentId = panel.ContentId;

            return new MenuItemMetadata
            {
                TargetName = MenuItemConstants.MainWindowTarget,
                OwnerGuid = IsMorePanel(panel) ? MorePanelsGuid : MenuItemConstants.View,
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

        private static bool IsMorePanel(RegisteredPanelInfo panel)
        {
            return panel.ContentId.StartsWith(ServiceLogPanelPrefix, StringComparison.Ordinal);
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
