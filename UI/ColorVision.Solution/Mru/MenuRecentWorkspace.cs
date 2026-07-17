using ColorVision.Common.MVVM;
using ColorVision.Solution.Editor;
using ColorVision.UI.Menus;
using System.IO;

namespace ColorVision.Solution.Mru
{
    public sealed class MenuRecentWorkspace : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.File;
        public override int Order => 80;
        public override string Header => Properties.Resources.RecentWorkspaces;
    }

    public sealed class MenuRecentWorkspaceProvider : IMenuItemProvider
    {
        private const int VisibleItemLimit = 10;

        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            var menuItems = new List<MenuItemMetadata>();
            foreach (MruPathEntry entry in SolutionManager.GetInstance()
                .RecentWorkspaces.Items.Take(VisibleItemLimit))
            {
                bool isAvailable = ResourceOpenService.TryDescribeWorkspaceResource(
                    entry.Path,
                    out WorkspaceResourceInfo resourceInfo);
                string displayName = isAvailable
                    ? resourceInfo.DisplayName
                    : GetDisplayName(entry.Path);
                string kindName = isAvailable ? resourceInfo.KindDisplayName : "不可用";
                string fullPath = isAvailable ? resourceInfo.FullPath : entry.Path;
                int index = menuItems.Count + 1;
                string indexText = index < 10 ? $"_{index}" : index.ToString();
                var command = new RelayCommand(
                    async _ => await ResourceOpenService.Instance.TryOpenWithFeedbackAsync(fullPath),
                    _ => isAvailable && SolutionManager.IsSupportedOpenPath(fullPath));
                menuItems.Add(new MenuItemMetadata
                {
                    OwnerGuid = nameof(MenuRecentWorkspace),
                    GuidId = fullPath,
                    Header = $"{indexText}  {(entry.IsPinned ? "📌 " : string.Empty)}{displayName}  [{kindName}] — {fullPath}",
                    Order = index - 1,
                    Command = command,
                });
            }
            return menuItems;
        }

        private static string GetDisplayName(string path)
        {
            string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            return string.IsNullOrWhiteSpace(name) ? path : name;
        }
    }
}
