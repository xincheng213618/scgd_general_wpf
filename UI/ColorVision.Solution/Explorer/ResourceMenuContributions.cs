using ColorVision.Common.MVVM;
using ColorVision.Solution.Editor;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Input;

namespace ColorVision.Solution.Explorer
{
    [SolutionMenuContribution(priority: 300)]
    public sealed class ResourceOpenMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.resource-open";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.Any;

        public bool IsApplicable(SolutionMenuContext context)
        {
            if (!context.IsMultipleSelection)
            {
                return context.PrimaryNode.CanOpen
                    || !string.IsNullOrWhiteSpace(context.PrimaryNode.EditorResourcePath);
            }
            return SolutionResourceOpenPolicy.CanOpen(context.Nodes);
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            SolutionNode node = context.PrimaryNode;
            var menuItems = new List<MenuItemMetadata>();
            bool canOpen = context.IsMultipleSelection
                ? SolutionResourceOpenPolicy.CanOpen(context.Nodes)
                : node.CanOpen;
            if (canOpen)
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionResourceCommands.OpenId,
                    Order = 1,
                    Header = ColorVision.Solution.Properties.Resources.MenuOpen,
                    Icon = MenuItemIcon.TryFindResource("DIOpen"),
                    InputGestureText = "Enter",
                    Command = SolutionResourceCommands.Open,
                });
            }

            if (!context.IsMultipleSelection
                && node.EditorResourcePath is { } resourcePath
                && ResourceOpenService.Instance.GetOpenWithEditors(resourcePath).Count > 0)
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionResourceCommands.OpenWithId,
                    Order = 2,
                    Header = $"{ColorVision.Solution.Properties.Resources.Sol_OpenAs}(_N)",
                    Command = SolutionResourceCommands.OpenWith,
                });
            }
            return menuItems;
        }
    }

    [SolutionMenuContribution(priority: 250)]
    public sealed class NodeManagementMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.node-management";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.SingleOnly;

        public bool IsApplicable(SolutionMenuContext context)
        {
            SolutionNode node = context.PrimaryNode;
            return node.CanRefresh || node.CanReName || node.CanShowProperties;
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            SolutionNode node = context.PrimaryNode;
            var menuItems = new List<MenuItemMetadata>();
            if (node.CanRefresh)
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionCommandIds.Refresh,
                    Order = 3,
                    Header = ColorVision.Solution.Properties.Resources.Refresh,
                    Command = NavigationCommands.Refresh,
                });
            }
            if (node.CanReName)
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionCommandIds.Rename,
                    Order = 104,
                    Header = ColorVision.UI.Properties.Resources.MenuRename,
                    Icon = MenuItemIcon.TryFindResource("DIRename"),
                    InputGestureText = "F2",
                    Command = Commands.ReName,
                });
            }
            if (node.CanShowProperties)
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionCommandIds.Properties,
                    Order = 9999,
                    Header = ColorVision.Solution.Properties.Resources.MenuProperty,
                    Icon = MenuItemIcon.TryFindResource("DIProperty"),
                    Command = ApplicationCommands.Properties,
                });
            }
            return menuItems;
        }
    }

    [SolutionMenuContribution(priority: 240)]
    public sealed class ContainerMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.container-actions";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.SingleOnly;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return context.PrimaryNode.CanAdd
                && context.PrimaryNode is ISolutionContainerNode container
                && container.SupportedContainerActions != SolutionContainerAction.None;
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            var container = (ISolutionContainerNode)context.PrimaryNode;
            var menuItems = new List<MenuItemMetadata>
            {
                new()
                {
                    GuidId = SolutionContainerCommands.AddMenuId,
                    Order = 10,
                    Header = ColorVision.Solution.Properties.Resources.MenuAdd,
                },
            };
            AddAction(
                menuItems,
                container,
                SolutionContainerAction.AddNewItem,
                SolutionContainerCommands.AddNewItemId,
                1,
                "新建项(_N)...",
                SolutionContainerCommands.AddNewItem);
            AddAction(
                menuItems,
                container,
                SolutionContainerAction.AddExistingItem,
                SolutionContainerCommands.AddExistingItemId,
                2,
                "现有项(_E)...",
                SolutionContainerCommands.AddExistingItem);
            AddAction(
                menuItems,
                container,
                SolutionContainerAction.CreateFolder,
                SolutionContainerCommands.CreateFolderId,
                10,
                ColorVision.Solution.Properties.Resources.AddFolder,
                SolutionContainerCommands.CreateFolder);
            AddAction(
                menuItems,
                container,
                SolutionContainerAction.AddNewProject,
                SolutionContainerCommands.AddNewProjectId,
                15,
                "新建项目(_P)...",
                SolutionContainerCommands.AddNewProject);
            AddAction(
                menuItems,
                container,
                SolutionContainerAction.AddExistingProject,
                SolutionContainerCommands.AddExistingProjectId,
                20,
                "现有项目(_E)...",
                SolutionContainerCommands.AddExistingProject);
            AddAction(
                menuItems,
                container,
                SolutionContainerAction.CreateSolutionFolder,
                SolutionContainerCommands.CreateSolutionFolderId,
                25,
                "新建解决方案文件夹(_F)",
                SolutionContainerCommands.CreateSolutionFolder);
            return menuItems;
        }

        private static void AddAction(
            List<MenuItemMetadata> menuItems,
            ISolutionContainerNode container,
            SolutionContainerAction action,
            string id,
            int order,
            string header,
            ICommand command)
        {
            if (!container.Supports(action))
                return;
            menuItems.Add(new MenuItemMetadata
            {
                OwnerGuid = SolutionContainerCommands.AddMenuId,
                GuidId = id,
                Order = order,
                Header = header,
                Command = command,
            });
        }
    }
}
