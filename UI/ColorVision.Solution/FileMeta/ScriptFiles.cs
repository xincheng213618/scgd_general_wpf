using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Solution.Terminal;
using ColorVision.UI.Menus;
using System.IO;

namespace ColorVision.Solution.FileMeta
{
    /// <summary>
    /// File meta for Python script files (.py, .pyw).
    /// Adds "Run in Terminal" context menu item.
    /// </summary>
    [FileMetaForExtension(".py|.pyw", name: "Python Script", isDefault: true)]
    public class PythonFile : FileMetaBase
    {
        public PythonFile() { }

        public PythonFile(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = fileInfo.Name;
            Icon = FileIcon.GetFileIconImageSource(fileInfo.FullName);
        }

        public override IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            var menuItems = base.GetMenuItems().ToList();

            menuItems.Add(new MenuItemMetadata
            {
                GuidId = "RunScript",
                Order = 0,
                Header = "运行脚本",
                Command = new RelayCommand(_ => TerminalService.GetInstance().RunScript(FileInfo.FullName)),
                Icon = MenuItemIcon.TryFindResource("DIRun")
            });

            return menuItems;
        }
    }

    /// <summary>
    /// File meta for PowerShell script files (.ps1).
    /// </summary>
    [FileMetaForExtension(".ps1", name: "PowerShell Script", isDefault: true)]
    public class PowerShellFile : FileMetaBase
    {
        public PowerShellFile() { }

        public PowerShellFile(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = fileInfo.Name;
            Icon = FileIcon.GetFileIconImageSource(fileInfo.FullName);
        }

        public override IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            var menuItems = base.GetMenuItems().ToList();

            menuItems.Add(new MenuItemMetadata
            {
                GuidId = "RunScript",
                Order = 0,
                Header = "运行脚本",
                Command = new RelayCommand(_ => TerminalService.GetInstance().RunScript(FileInfo.FullName)),
                Icon = MenuItemIcon.TryFindResource("DIRun")
            });

            return menuItems;
        }
    }

    /// <summary>
    /// File meta for batch/cmd script files (.bat, .cmd).
    /// </summary>
    [FileMetaForExtension(".bat|.cmd", name: "Batch Script", isDefault: true)]
    public class BatchFile : FileMetaBase
    {
        public BatchFile() { }

        public BatchFile(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = fileInfo.Name;
            Icon = FileIcon.GetFileIconImageSource(fileInfo.FullName);
        }

        public override IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            var menuItems = base.GetMenuItems().ToList();

            menuItems.Add(new MenuItemMetadata
            {
                GuidId = "RunScript",
                Order = 0,
                Header = "运行脚本",
                Command = new RelayCommand(_ => TerminalService.GetInstance().RunScript(FileInfo.FullName)),
                Icon = MenuItemIcon.TryFindResource("DIRun")
            });

            return menuItems;
        }
    }
}
