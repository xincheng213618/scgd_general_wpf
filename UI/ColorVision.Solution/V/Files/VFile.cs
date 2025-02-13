using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.UI.Menus;
using ColorVision.Solution.Properties;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using ColorVision.UI;
using System.Windows;
using ColorVision.Common.Utilities;
using System.Collections.Generic;

namespace ColorVision.Solution.V.Files
{
    public class VFile : VObject
    {
        public IFileMeta FileMeta { get; set; }
        public RelayCommand OpenContainingFolderCommand { get; set; }
        public RelayCommand CopyFullPathCommand { get; set; }

        public FileInfo FileInfo { get => FileMeta.FileInfo; set { FileMeta.FileInfo = value; } }

        public VFile(IFileMeta fileMeta) :base()
        {
            FileMeta = fileMeta;
            ToolTip = fileMeta.ToolTip;
            Name1 = fileMeta.Name;
            Icon = fileMeta.Icon;
            OpenContainingFolderCommand = new RelayCommand(a => PlatformHelper.OpenFolderAndSelectFile(FileInfo.FullName), a => FileInfo.Exists);
            CopyFullPathCommand = new RelayCommand(a => Common.NativeMethods.Clipboard.SetText(FileInfo.FullName), a => FileInfo.Exists);
        }
        public override void InitMenuItem()
        {
            base.InitMenuItem();
            MenuItemMetadatas.AddRange(FileMeta.GetMenuItems());
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Open", Order = 1, Command = OpenCommand, Header = Resources.MenuOpen, Icon = MenuItemIcon.TryFindResource("DIOpen") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "CopyFullPath", Order = 200, Command = CopyFullPathCommand, Header = ColorVision.Solution.Properties.Resources.MenuCopyFullPath , Icon = MenuItemIcon.TryFindResource("DICopy") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "OpenContainingFolder", Order = 200, Header = Resources.MenuOpenContainingFolder, Command = OpenContainingFolderCommand });
        }
        public override void ShowProperty()
        {
            FileProperties.ShowFileProperties(FileInfo.FullName);
        }

        public override void Open()
        {
            FileMeta.Open();
        }

        public override void Delete()
        {
            try
            {
                File.Delete(FileInfo.FullName);
                base.Delete();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        public override bool ReName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) { MessageBox.Show("路径地址不允许为空"); return false; }
            try
            {
                if (FileInfo.Directory != null)
                {

                    string destinationDirectoryPath = Path.Combine(FileInfo.Directory.FullName, name);
                    File.Move(FileInfo.FullName, destinationDirectoryPath);
                    FileInfo =  new FileInfo(destinationDirectoryPath);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }
    }
}
