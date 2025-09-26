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
    /// <summary>
    /// Visual representation of a file in the solution explorer.
    /// Provides file-specific operations like opening with different editors, 
    /// opening containing folder, etc.
    /// </summary>
    public class VFile : VObject
    {
        public IFileMeta FileMeta { get; set; }
        public RelayCommand OpenContainingFolderCommand { get; set; }

        public FileInfo FileInfo { get => FileMeta.FileInfo; set { FileMeta.FileInfo = value; } }

        public RelayCommand OpenMethodCommand { get; set; }

        public VFile(IFileMeta fileMeta) :base()
        {
            FileMeta = fileMeta;
            Name1 = fileMeta.Name;
            Icon = fileMeta.Icon;
            FullPath = FileInfo.FullName;
            Initialize();
        }

        public override void Initialize()
        {
            base.Initialize();
            OpenContainingFolderCommand = new RelayCommand(a => PlatformHelper.OpenFolderAndSelectFile(FileInfo.FullName), a => FileInfo.Exists);
            OpenMethodCommand = new RelayCommand(a => OpenMethod());
        }

        public void OpenMethod()
        {
            var ext = Path.GetExtension(FullPath);
            var types = EditorManager.Instance.GetEditorsForExt(ext);
            var current = EditorManager.Instance.GetDefaultEditorType(ext);

            if (types.Count == 0) return;

            var window = new EditorSelectionWindow(types, current, FullPath) { Owner = Application.Current.GetActiveWindow() , WindowStartupLocation =WindowStartupLocation.CenterScreen};
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
                LogOperation($"开始删除文件: {FileInfo.FullName}");
                File.Delete(FileInfo.FullName);
                LogOperation($"成功删除文件: {FileInfo.FullName}");
                base.Delete();
            }
            catch (UnauthorizedAccessException ex)
            {
                LogError($"删除文件失败 - 权限不足: {ex.Message}", ex);
                ShowUserError("权限不足，无法删除文件");
            }
            catch (FileNotFoundException ex)
            {
                LogError($"删除文件失败 - 文件未找到: {ex.Message}", ex);
                ShowUserError("文件不存在，可能已被删除");
                base.Delete(); // 从界面移除
            }
            catch (IOException ex)
            {
                LogError($"删除文件失败 - IO错误: {ex.Message}", ex);
                ShowUserError($"删除文件失败: {ex.Message}");
            }
            catch (Exception ex)
            {
                LogError($"删除文件失败 - 未知错误: {ex.Message}", ex);
                ShowUserError($"删除失败: {ex.Message}");
            }
        }
        
        public override bool ReName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) 
            { 
                ShowUserError("文件名不允许为空");
                return false; 
            }

            string? originalPath = null;
            FileInfo? originalFileInfo = null;

            try
            {
                if (FileInfo.Directory != null)
                {
                    originalPath = FileInfo.FullName;
                    originalFileInfo = new FileInfo(originalPath);
                    
                    LogOperation($"开始重命名文件: {originalPath} -> {name}");

                    string destinationFilePath = Path.Combine(FileInfo.Directory.FullName, name);
                    
                    // 检查目标文件是否已存在
                    if (File.Exists(destinationFilePath))
                    {
                        ShowUserError($"目标文件 '{name}' 已存在");
                        return false;
                    }

                    File.Move(FileInfo.FullName, destinationFilePath);
                    FileInfo = new FileInfo(destinationFilePath);
                    FullPath = destinationFilePath;
                    
                    LogOperation($"成功重命名文件: {originalPath} -> {destinationFilePath}");
                    return true;
                }
                else
                {
                    ShowUserError("无法获取文件目录信息");
                    return false;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogError($"重命名文件失败 - 权限不足: {ex.Message}", ex);
                ShowUserError("权限不足，无法重命名文件");
                return RollbackFileRename(originalPath, originalFileInfo);
            }
            catch (FileNotFoundException ex)
            {
                LogError($"重命名文件失败 - 文件未找到: {ex.Message}", ex);
                ShowUserError("源文件不存在");
                return false;
            }
            catch (IOException ex)
            {
                LogError($"重命名文件失败 - IO错误: {ex.Message}", ex);
                ShowUserError($"文件重命名失败: {ex.Message}");
                return RollbackFileRename(originalPath, originalFileInfo);
            }
            catch (Exception ex)
            {
                LogError($"重命名文件失败 - 未知错误: {ex.Message}", ex);
                ShowUserError($"重命名失败: {ex.Message}");
                return RollbackFileRename(originalPath, originalFileInfo);
            }
        }

        private bool RollbackFileRename(string? originalPath, FileInfo? originalFileInfo)
        {
            try
            {
                if (originalFileInfo != null && originalPath != null)
                {
                    LogOperation($"尝试回滚文件重命名操作: {originalPath}");
                    FileInfo = originalFileInfo;
                    FullPath = originalPath;
                    LogOperation("成功回滚文件重命名操作");
                }
            }
            catch (Exception rollbackEx)
            {
                LogError($"回滚文件重命名操作失败: {rollbackEx.Message}", rollbackEx);
                ShowUserError("回滚操作也失败了，文件状态可能不一致");
            }
            
            return false;
        }
    }
}
