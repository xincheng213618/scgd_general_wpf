using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Common.Utilities;
using ColorVision.Solution.Editor;
using ColorVision.Solution.FileMeta;
using ColorVision.Solution.Properties;
using ColorVision.UI.Menus;
using System.IO;
using System.Windows;

namespace ColorVision.Solution.V
{
    public class VFile : VObject
    {
        public IFileMeta FileMeta { get; set; }
        public RelayCommand OpenContainingFolderCommand { get; set; }
        public RelayCommand CopyFullPathCommand { get; set; }

        public FileInfo FileInfo { get => FileMeta.FileInfo; set { FileMeta.FileInfo = value; } }


        public RelayCommand OpenMethodCommand { get; set; }

        public VFile(IFileMeta fileMeta) :base()
        {
            FileMeta = fileMeta;
            Name1 = fileMeta.Name;
            Icon = fileMeta.Icon;
            FullPath = FileInfo.FullName;
            OpenContainingFolderCommand = new RelayCommand(a => PlatformHelper.OpenFolderAndSelectFile(FileInfo.FullName), a => FileInfo.Exists);
            CopyFullPathCommand = new RelayCommand(a => Common.NativeMethods.Clipboard.SetText(FileInfo.FullName), a => FileInfo.Exists);
            OpenMethodCommand = new RelayCommand(a => OpenMethod());
        }

        public void OpenMethod()
        {
            var ext = Path.GetExtension(FullPath);
            var types = EditorManager.Instance.GetEditorsForExt(ext);
            var current = EditorManager.Instance.GetDefaultEditorType(ext);

            if (types.Count == 0) return;

            var window = new EditorSelectionWindow(types, current) { Owner = Application.Current.GetActiveWindow() , WindowStartupLocation =WindowStartupLocation.CenterScreen};
            if (window.ShowDialog() == true)
            {
                var selectedType = window.SelectedEditorType;
                EditorManager.Instance.SetDefaultEditor(ext, selectedType);
            }
        }


        public override void InitMenuItem()
        {
            base.InitMenuItem();
            MenuItemMetadatas.AddRange(FileMeta.GetMenuItems());
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Open", Order = 1, Command = OpenCommand, Header = Resources.MenuOpen, Icon = MenuItemIcon.TryFindResource("DIOpen") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "OpenMethod", Order = 2, Command = OpenMethodCommand, Header = "打开方式(_N)" });

            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "CopyFullPath", Order = 200, Command = CopyFullPathCommand, Header = Resources.MenuCopyFullPath , Icon = MenuItemIcon.TryFindResource("DICopy") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "OpenContainingFolder", Order = 200, Header = Resources.MenuOpenContainingFolder, Command = OpenContainingFolderCommand });
        }
        public override void ShowProperty()
        {
            FileProperties.ShowFileProperties(FileInfo.FullName);
        }

        public override void Open()
        {
            var IEditor = EditorManager.Instance.OpenFile(FullPath);
            IEditor?.Open(FullPath);
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
