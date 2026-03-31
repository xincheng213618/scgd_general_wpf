using ColorVision.Solution.Explorer;
using ColorVision.UI;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Solution
{
    public partial class TreeViewControl
    {
        private const string ClipboardFormat = "SolutionNodePath";

        private void IniCommand()
        {
            SolutionTreeView.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ExecutedCommand, CanExecuteCommand));
            SolutionTreeView.CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut, ExecutedCommand, CanExecuteCommand));
            SolutionTreeView.CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, ExecutedCommand, CanExecuteCommand));

            SolutionTreeView.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) =>
            {
                if (SelectedTreeViewItem?.DataContext is SolutionNode baseObject) baseObject.Delete();
            }
            , (s, e) => e.CanExecute = SelectedTreeViewItem != null && SelectedTreeViewItem.DataContext is SolutionNode baseObject && baseObject.CanDelete));


            SolutionTreeView.CommandBindings.Add(new CommandBinding(Commands.ReName, (s, e) =>
            {
                if (SelectedTreeViewItem != null && SelectedTreeViewItem.DataContext is SolutionNode baseObject)
                    baseObject.IsEditMode = true;
            }, (s, e) => e.CanExecute = SelectedTreeViewItem != null && SelectedTreeViewItem.DataContext is SolutionNode baseObject && baseObject.CanReName));
        }

        #region 通用命令执行函数
        private void CanExecuteCommand(object sender, CanExecuteRoutedEventArgs e)
        {
            var node = e.Parameter as SolutionNode
                ?? SelectedTreeViewItem?.DataContext as SolutionNode;

            if (node == null)
                return;

            if (e.Command == ApplicationCommands.SelectAll)
            {
                e.CanExecute = false;
            }
            else if (e.Command == ApplicationCommands.Copy)
            {
                e.CanExecute = node.CanCopy && !string.IsNullOrEmpty(node.FullPath);
            }
            else if (e.Command == ApplicationCommands.Cut)
            {
                e.CanExecute = node.CanCut && !string.IsNullOrEmpty(node.FullPath);
            }
            else if (e.Command == ApplicationCommands.Paste)
            {
                e.CanExecute = node.CanPaste && Clipboard.ContainsData(ClipboardFormat);
            }
        }

        private void ExecutedCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command == ApplicationCommands.Copy)
            {
                var node = e.Parameter as SolutionNode
                    ?? SelectedTreeViewItem?.DataContext as SolutionNode;
                if (node != null && !string.IsNullOrEmpty(node.FullPath))
                {
                    Clipboard.SetData(ClipboardFormat, node.FullPath);
                }
            }
            else if (e.Command == ApplicationCommands.Cut)
            {
                var node = e.Parameter as SolutionNode
                    ?? SelectedTreeViewItem?.DataContext as SolutionNode;
                if (node != null && !string.IsNullOrEmpty(node.FullPath))
                {
                    Clipboard.SetData(ClipboardFormat, node.FullPath);
                }
            }
            else if (e.Command == ApplicationCommands.Paste)
            {
                if (Clipboard.ContainsData(ClipboardFormat))
                {
                    var sourcePath = Clipboard.GetData(ClipboardFormat) as string;
                    var targetNode = SelectedTreeViewItem?.DataContext as SolutionNode;
                    if (!string.IsNullOrEmpty(sourcePath) && targetNode != null)
                    {
                        string targetDir = targetNode.FullPath;
                        if (targetNode is FileNode)
                            targetDir = Path.GetDirectoryName(targetNode.FullPath);

                        if (!string.IsNullOrEmpty(targetDir) && Directory.Exists(targetDir))
                        {
                            if (File.Exists(sourcePath))
                            {
                                var destPath = Path.Combine(targetDir, Path.GetFileName(sourcePath));
                                if (!File.Exists(destPath))
                                    File.Copy(sourcePath, destPath);
                            }
                            else if (Directory.Exists(sourcePath))
                            {
                                var destPath = Path.Combine(targetDir, Path.GetFileName(sourcePath));
                                if (!Directory.Exists(destPath))
                                    CopyDirectory(sourcePath, destPath);
                            }
                        }
                    }
                }
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);
            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)));
            foreach (var dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(destinationDir, Path.GetFileName(dir)));
        }

        #endregion
    }

}