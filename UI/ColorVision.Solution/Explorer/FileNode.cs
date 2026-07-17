#pragma warning disable CS8604
using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Common.Utilities;
using ColorVision.Solution.Editor;
using ColorVision.Solution.FileMeta;
using ColorVision.Solution.Properties;
using ColorVision.Solution.Workspace;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.IO;
using System.Windows;

namespace ColorVision.Solution.Explorer
{
    public class FileNode : SolutionNode
    {
        public IFileMeta FileMeta { get; set; }
        public RelayCommand OpenContainingFolderCommand { get; set; }
        public RelayCommand AskCopilotExplainFileCommand { get; set; }
        public RelayCommand AskCopilotDiagnoseFileCommand { get; set; }

        public FileInfo FileInfo { get => FileMeta.FileInfo; set { FileMeta.FileInfo = value; } }

        public RelayCommand OpenMethodCommand { get; set; }

        public FileNode(IFileMeta fileMeta) : base()
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
            AskCopilotExplainFileCommand = new RelayCommand(a => AskCopilotAboutFile(CopilotPromptMode.Code, false), a => FileInfo.Exists);
            AskCopilotDiagnoseFileCommand = new RelayCommand(a => AskCopilotAboutFile(CopilotPromptMode.Diagnose, true), a => FileInfo.Exists);
        }

        public void OpenMethod()
        {
            var ext = Path.GetExtension(FullPath);
            var descriptors = EditorManager.Instance.GetFileEditorDescriptors(ext, visibleOnly: true);
            var current = EditorManager.Instance.GetDefaultFileEditorDescriptor(ext);

            if (descriptors.Count == 0) return;

            var window = new EditorSelectionWindow(descriptors, current?.Id, FullPath) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            if (window.ShowDialog() == true && window.SelectedEditor is { } selectedEditor)
            {
                if (window.AlwaysUseSelectedEditor)
                    EditorManager.Instance.SetDefaultEditor(ext, selectedEditor.Id);
                EditorManager.Instance.OpenFileWith(FullPath, selectedEditor.Id);
            }
        }

        public override void InitMenuItem()
        {
            base.InitMenuItem();
            MenuItemMetadatas.AddRange(FileMeta.GetMenuItems());
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Open", Order = 1, Command = OpenCommand, Header = Resources.MenuOpen, Icon = MenuItemIcon.TryFindResource("DIOpen") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "OpenMethod", Order = 2, Command = OpenMethodCommand, Header = "打开方式(_N)" });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "AskCopilotExplainFile", Order = 20, Header = "问 AI 解释此文件", Command = AskCopilotExplainFileCommand });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "AskCopilotDiagnoseFile", Order = 21, Header = "问 AI 诊断此文件/日志", Command = AskCopilotDiagnoseFileCommand });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "OpenContainingFolder", Order = 200, Header = Resources.MenuOpenContainingFolder, Command = OpenContainingFolderCommand });
        }

        private void AskCopilotAboutFile(CopilotPromptMode mode, bool diagnose)
        {
            if (!FileInfo.Exists)
                return;

            var contextItem = BuildCopilotFileContextItem();
            CopilotLiveContextRegistry.Publish(new CopilotLiveContext
            {
                SourceId = "solution-file-node",
                Title = contextItem.Title,
                Summary = contextItem.Summary,
                AttachmentTitle = contextItem.Title,
                SnapshotItems = new[] { contextItem },
            });

            var result = CopilotPromptRequestHelper.Dispatch(new CopilotPromptRequestOptions
                {
                    Mode = mode,
                    Prompt = diagnose
                        ? $"请诊断这个文件是否包含异常、失败线索或需要优先关注的问题。文件路径：{FileInfo.FullName}。如果它是日志，请提取关键错误；如果它是代码或配置，请指出主要风险和下一步排查建议。"
                        : $"请解释这个文件在当前工作区中的作用、主要结构和建议优先阅读的部分。文件路径：{FileInfo.FullName}。如有必要，请直接读取该文件后再回答。",
                    ContextItems = new[] { contextItem },
                });

            if (!result.WasSent)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), result.StatusMessage, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private CopilotContextItem BuildCopilotFileContextItem()
        {
            return new CopilotContextItem
            {
                Id = "solution-file-node",
                Title = "Selected solution file",
                Summary = FileInfo.Name,
                Content = string.Join(Environment.NewLine, new[]
                {
                    $"Path: {FileInfo.FullName}",
                    $"Extension: {FileInfo.Extension}",
                    $"Size bytes: {FileInfo.Length}",
                    $"Last modified: {FileInfo.LastWriteTime:O}",
                }),
            };
        }

        public override void ShowProperty()
        {
            FileProperties.ShowFileProperties(FileInfo.FullName);
        }

        public override void Open()
        {
            EditorManager.Instance.TryOpenFile(FullPath);
        }

        public override void Delete()
        {
            TryDelete(showConfirmation: true);
        }

        internal override bool TryDelete(bool showConfirmation)
        {
            if (showConfirmation
                && MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"确定将“{Name}”移到回收站吗？",
                    "ColorVision",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning) != MessageBoxResult.OK)
            {
                return false;
            }

            if (!EditorDocumentService.TryCloseDocumentsForResources([FileInfo.FullName]))
                return false;

            try
            {
                LogOperation($"开始删除文件到回收站: {FileInfo.FullName}");
                int result = ShellFileOperations.DeleteToRecycleBin(FileInfo.FullName);
                if (result != 0)
                {
                    ShowUserError($"删除文件失败，Shell 返回代码: {result}");
                    return false;
                }
                LogOperation($"成功删除文件到回收站: {FileInfo.FullName}");
                base.TryDelete(showConfirmation: false);
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                LogError($"删除文件失败 - 权限不足: {ex.Message}", ex);
                ShowUserError("权限不足，无法删除文件");
                return false;
            }
            catch (FileNotFoundException ex)
            {
                LogError($"删除文件失败 - 文件未找到: {ex.Message}", ex);
                ShowUserError("文件不存在，可能已被删除");
                base.TryDelete(showConfirmation: false);
                return true;
            }
            catch (IOException ex)
            {
                LogError($"删除文件失败 - IO错误: {ex.Message}", ex);
                ShowUserError($"删除文件失败: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LogError($"删除文件失败 - 未知错误: {ex.Message}", ex);
                ShowUserError($"删除失败: {ex.Message}");
                return false;
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

                    if (File.Exists(destinationFilePath))
                    {
                        ShowUserError($"目标文件 '{name}' 已存在");
                        return false;
                    }

                    if (!EditorDocumentService.TryPrepareResourceRename(originalPath))
                        return false;

                    File.Move(FileInfo.FullName, destinationFilePath);
                    EditorDocumentService.NotifyResourceRenamed(originalPath, destinationFilePath);
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
