using ColorVision.Common.NativeMethods;
using ColorVision.UI.Menus;
using System.IO;
using System.Windows;
using System.Windows.Media;

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
        private static ImageSource? _projectIcon;

        private static ImageSource GetProjectIcon()
        {
            if (_projectIcon != null) return _projectIcon;

            // Draw a folder with a small gear badge to distinguish from regular folders
            var group = new DrawingGroup();

            // Folder body (golden/amber color)
            var folderBody = new GeometryDrawing(
                new SolidColorBrush(Color.FromRgb(0xDC, 0xA0, 0x2E)),
                new Pen(new SolidColorBrush(Color.FromRgb(0xC0, 0x8C, 0x28)), 0.3),
                Geometry.Parse("M 1,4 L 1,14 L 15,14 L 15,5 L 8,5 L 6.5,3 L 1,3 Z"));

            // Folder tab
            var folderTab = new GeometryDrawing(
                new SolidColorBrush(Color.FromRgb(0xE8, 0xB0, 0x3E)),
                null,
                Geometry.Parse("M 1,3 L 6.5,3 L 8,5 L 1,5 Z"));

            // Small "P" badge for project (dark circle with P letter)
            var badgeBg = new GeometryDrawing(
                new SolidColorBrush(Color.FromRgb(0x40, 0x80, 0xD0)),
                null,
                new EllipseGeometry(new Point(12.5, 11), 3, 3));

            var badgeText = new GeometryDrawing(
                Brushes.White,
                null,
                Geometry.Parse("M 11.2,9.2 L 11.2,12.8 M 11.2,9.2 L 12.8,9.2 C 13.6,9.2 14,9.8 14,10.4 C 14,11 13.6,11.4 12.8,11.4 L 11.2,11.4"));
            badgeText.Pen = new Pen(Brushes.White, 0.6);

            group.Children.Add(folderBody);
            group.Children.Add(folderTab);
            group.Children.Add(badgeBg);
            group.Children.Add(badgeText);

            var image = new DrawingImage(group);
            image.Freeze();
            _projectIcon = image;
            return _projectIcon;
        }

        public ProjectFolder() { }

        public ProjectFolder(DirectoryInfo directoryInfo)
        {
            DirectoryInfo = directoryInfo;
            Icon = GetProjectIcon();
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