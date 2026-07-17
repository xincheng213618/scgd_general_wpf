using ColorVision.Common.MVVM;
using ColorVision.Solution.Editor;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Input;

namespace ColorVision.Solution.Explorer
{
    [SolutionMenuContribution(priority: 210)]
    public sealed class DeleteMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.delete";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.Any;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return context.Nodes.Count > 0 && context.Nodes.All(node => node.CanDelete);
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            string header = context.Nodes.Count == 1
                ? GetDeleteHeader(context.PrimaryNode.DeleteKind)
                : ColorVision.UI.Properties.Resources.MenuDelete;
            return
            [
                new MenuItemMetadata
                {
                    GuidId = SolutionCommandIds.Delete,
                    Order = 103,
                    Command = ApplicationCommands.Delete,
                    Header = header,
                    Icon = MenuItemIcon.TryFindResource("DIDelete"),
                    InputGestureText = "Del",
                },
            ];
        }

        private static string GetDeleteHeader(SolutionDeleteKind deleteKind)
        {
            return deleteKind switch
            {
                SolutionDeleteKind.RemoveFromSolution => "从解决方案中移除(_V)",
                SolutionDeleteKind.RemoveSolutionFolder => "移除解决方案文件夹(_V)",
                _ => ColorVision.UI.Properties.Resources.MenuDelete,
            };
        }
    }

    [SolutionMenuContribution(priority: 200)]
    public sealed class ClipboardMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.clipboard";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.Any;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return CanCopy(context.Nodes)
                || CanCut(context.Nodes)
                || (!context.IsMultipleSelection && context.PrimaryNode.CanPaste);
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            var menuItems = new List<MenuItemMetadata>();
            if (CanCut(context.Nodes))
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionCommandIds.Cut,
                    Order = 100,
                    Command = ApplicationCommands.Cut,
                    Header = ColorVision.UI.Properties.Resources.MenuCut,
                    Icon = MenuItemIcon.TryFindResource("DICut"),
                    InputGestureText = "Ctrl+X",
                });
            }
            if (CanCopy(context.Nodes))
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionCommandIds.Copy,
                    Order = 101,
                    Command = ApplicationCommands.Copy,
                    Header = ColorVision.UI.Properties.Resources.MenuCopy,
                    Icon = MenuItemIcon.TryFindResource("DICopy"),
                    InputGestureText = "Ctrl+C",
                });
            }
            if (!context.IsMultipleSelection && context.PrimaryNode.CanPaste)
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionCommandIds.Paste,
                    Order = 102,
                    Command = ApplicationCommands.Paste,
                    Header = ColorVision.UI.Properties.Resources.MenuPaste,
                    Icon = MenuItemIcon.TryFindResource("DIPaste"),
                    InputGestureText = "Ctrl+V",
                });
            }
            return menuItems;
        }

        private static bool CanCopy(IReadOnlyList<SolutionNode> nodes)
        {
            return nodes.Count > 0
                && nodes.All(node => node.CanCopy && node.ClipboardResourcePath != null);
        }

        private static bool CanCut(IReadOnlyList<SolutionNode> nodes)
        {
            return nodes.Count > 0
                && nodes.All(node => node.CanCut && node.ClipboardResourcePath != null);
        }
    }

    [SolutionMenuContribution(priority: 100)]
    public sealed class CopyFullPathMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.copy-full-path";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.SingleOnly;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return !string.IsNullOrWhiteSpace(context.PrimaryNode.FullPath);
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            SolutionNode node = context.PrimaryNode;
            return
            [
                new MenuItemMetadata
                {
                    GuidId = "CopyFullPath",
                    Order = 200,
                    Header = ColorVision.Solution.Properties.Resources.CopyFullPath,
                    Icon = MenuItemIcon.TryFindResource("DICopyFullPath"),
                    Command = new RelayCommand(_ => node.CopyFullPath()),
                },
            ];
        }
    }
}
