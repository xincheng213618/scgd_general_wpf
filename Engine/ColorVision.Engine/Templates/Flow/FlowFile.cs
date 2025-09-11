using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Solution.FileMeta;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace ColorVision.Engine.Templates.Flow
{
    [FileExtension(".stn", ".cvflow")]
    public class FlowFile : FileMetaBase
    {
        public FlowFile(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = FileInfo.Name;  
            Icon = FileIcon.GetFileIconImageSource(fileInfo.FullName);
        }

        public override IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            RelayCommand relayCommand = new RelayCommand(a => MessageBox.Show("@2222"));
            MenuItemMetadata menuItemMetadata = new MenuItemMetadata() {  Header = "测试", Command = relayCommand };

            return new List<MenuItemMetadata>(){ menuItemMetadata };
        }
    }

}
