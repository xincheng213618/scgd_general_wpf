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
        private bool _isCutOperation;

        private void IniCommand()
        {
            // Add command bindings to both the TreeView and the UserControl
            // so keyboard shortcuts work regardless of internal focus state
            var copyBinding = new CommandBinding(ApplicationCommands.Copy, ExecutedCommand, CanExecuteCommand);
            var cutBinding = new CommandBinding(ApplicationCommands.Cut, ExecutedCommand, CanExecuteCommand);
            var pasteBinding = new CommandBinding(ApplicationCommands.Paste, ExecutedCommand, CanExecuteCommand);

            SolutionTreeView.CommandBindings.Add(copyBinding);
            SolutionTreeView.CommandBindings.Add(cutBinding);
            SolutionTreeView.CommandBindings.Add(pasteBinding);
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ExecutedCommand, CanExecuteCommand));
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut, ExecutedCommand, CanExecuteCommand));
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, ExecutedCommand, CanExecuteCommand));

            SolutionTreeView.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) =>
            {
                // Multi-select delete
                var toDelete = _selectedNodes.Where(n => n.CanDelete).ToList();
                foreach (var node in toDelete)
                    node.Delete();
            }
            , (s, e) => e.CanExecute = _selectedNodes.Any(n => n.CanDelete)));

            SolutionTreeView.CommandBindings.Add(new CommandBinding(Commands.ReName, (s, e) =>
            {
                // Rename only works on single selection
                if (_selectedNodes.Count == 1 && _selectedNodes[0].CanReName)
                    _selectedNodes[0].IsEditMode = true;
            }, (s, e) => e.CanExecute = _selectedNodes.Count == 1 && _selectedNodes[0].CanReName));
        }

        #region Command Handlers

        private void CanExecuteCommand(object sender, CanExecuteRoutedEventArgs e)
        {
            if (_selectedNodes.Count == 0)
                return;

            if (e.Command == ApplicationCommands.Copy)
            {
                e.CanExecute = _selectedNodes.All(n => n.CanCopy && !string.IsNullOrEmpty(n.FullPath));
            }
            else if (e.Command == ApplicationCommands.Cut)
            {
                e.CanExecute = _selectedNodes.All(n => n.CanCut && !string.IsNullOrEmpty(n.FullPath));
            }
            else if (e.Command == ApplicationCommands.Paste)
            {
                e.CanExecute = _selectedNodes.Count == 1
                    && _selectedNodes[0].CanPaste
                    && Clipboard.ContainsData(ClipboardFormat);
            }
        }

        private void ExecutedCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command == ApplicationCommands.Copy)
            {
                var paths = _selectedNodes
                    .Where(n => !string.IsNullOrEmpty(n.FullPath))
                    .Select(n => n.FullPath)
                    .ToArray();
                if (paths.Length > 0)
                {
                    Clipboard.SetData(ClipboardFormat, paths);
                    _isCutOperation = false;
                }
            }
            else if (e.Command == ApplicationCommands.Cut)
            {
                var paths = _selectedNodes
                    .Where(n => !string.IsNullOrEmpty(n.FullPath))
                    .Select(n => n.FullPath)
                    .ToArray();
                if (paths.Length > 0)
                {
                    Clipboard.SetData(ClipboardFormat, paths);
                    _isCutOperation = true;
                }
            }
            else if (e.Command == ApplicationCommands.Paste)
            {
                if (!Clipboard.ContainsData(ClipboardFormat) || _selectedNodes.Count == 0)
                    return;

                var data = Clipboard.GetData(ClipboardFormat);
                string[] sourcePaths;
                if (data is string singlePath)
                    sourcePaths = new[] { singlePath };
                else if (data is string[] paths)
                    sourcePaths = paths;
                else
                    return;

                var targetNode = _selectedNodes[0];
                string targetDir = targetNode.FullPath;
                if (targetNode is FileNode)
                    targetDir = Path.GetDirectoryName(targetNode.FullPath) ?? targetDir;

                if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
                    return;

                foreach (var sourcePath in sourcePaths)
                {
                    if (File.Exists(sourcePath))
                    {
                        var destPath = Path.Combine(targetDir, Path.GetFileName(sourcePath));
                        if (!File.Exists(destPath))
                        {
                            if (_isCutOperation)
                                File.Move(sourcePath, destPath);
                            else
                                File.Copy(sourcePath, destPath);
                        }
                    }
                    else if (Directory.Exists(sourcePath))
                    {
                        var destPath = Path.Combine(targetDir, Path.GetFileName(sourcePath));
                        if (!Directory.Exists(destPath))
                        {
                            if (_isCutOperation)
                                Directory.Move(sourcePath, destPath);
                            else
                                CopyDirectory(sourcePath, destPath);
                        }
                    }
                }

                if (_isCutOperation)
                {
                    Clipboard.Clear();
                    _isCutOperation = false;
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