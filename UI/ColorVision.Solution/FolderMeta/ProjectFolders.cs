using ColorVision.Common.NativeMethods;
using ColorVision.UI.Menus;
using System.IO;

namespace ColorVision.Solution.FolderMeta
{
    /// <summary>
    /// Example folder meta for project directories.
    /// Demonstrates the new pattern-based registration system for folders.
    /// This will be applied to any folder containing "project" in its name.
    /// </summary>
    [FolderMetaForPattern("project", name: "Project Folder", isDefault: false)]
    public class ProjectFolder : FolderMetaBase
    {
        public ProjectFolder() { }

        public ProjectFolder(DirectoryInfo directoryInfo)
        {
            DirectoryInfo = directoryInfo;
            Icon = FileIcon.GetDirectoryIconImageSource();
        }

        public override IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            var menuItems = base.GetMenuItems().ToList();
            
            // Add project-specific menu items
            menuItems.Add(new MenuItemMetadata 
            { 
                GuidId = "BuildProject", 
                Order = 1, 
                Header = "构建项目", 
                Icon = MenuItemIcon.TryFindResource("DIBuild") 
            });
            
            menuItems.Add(new MenuItemMetadata 
            { 
                GuidId = "OpenInIDE", 
                Order = 2, 
                Header = "在IDE中打开", 
                Icon = MenuItemIcon.TryFindResource("DICode") 
            });
            
            return menuItems;
        }
    }

    /// <summary>
    /// Example folder meta for solution directories.
    /// This will be applied to any folder with ".sln" files or containing "solution" in its name.
    /// </summary>
    [FolderMetaForPattern("solution|\\.sln", name: "Solution Folder", isDefault: false)]
    public class SolutionFolder : FolderMetaBase
    {
        public SolutionFolder() { }

        public SolutionFolder(DirectoryInfo directoryInfo)
        {
            DirectoryInfo = directoryInfo;
            Icon = FileIcon.GetDirectoryIconImageSource();
        }

        public override IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            var menuItems = base.GetMenuItems().ToList();
            
            // Add solution-specific menu items
            menuItems.Add(new MenuItemMetadata 
            { 
                GuidId = "BuildSolution", 
                Order = 1, 
                Header = "构建解决方案", 
                Icon = MenuItemIcon.TryFindResource("DIBuildAll") 
            });
            
            menuItems.Add(new MenuItemMetadata 
            { 
                GuidId = "ManageNuget", 
                Order = 2, 
                Header = "管理NuGet包", 
                Icon = MenuItemIcon.TryFindResource("DINuGet") 
            });
            
            return menuItems;
        }
    }
}