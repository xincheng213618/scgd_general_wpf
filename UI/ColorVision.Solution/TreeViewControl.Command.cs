using ColorVision.Solution.Explorer;
using ColorVision.UI;
using System.Collections.Specialized;
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
                var toDelete = _selectedNodes
                    .Where(n => n.CanDelete && !_selectedNodes.Any(parent => !ReferenceEquals(parent, n) && IsAncestorOf(parent, n)))
                    .ToList();
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
                    && HasClipboardPaths();
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
                    SetClipboardPaths(paths, isCut: false);
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
                    SetClipboardPaths(paths, isCut: true);
                    _isCutOperation = true;
                }
            }
            else if (e.Command == ApplicationCommands.Paste)
            {
                if (!TryGetClipboardPaths(out var sourcePaths, out bool isCut, out bool isInternalClipboard) || _selectedNodes.Count == 0)
                    return;

                var targetNode = _selectedNodes[0];
                string targetDir = targetNode.FullPath;
                if (targetNode is FileNode)
                    targetDir = Path.GetDirectoryName(targetNode.FullPath) ?? targetDir;

                if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
                    return;

                CopyOrMovePaths(sourcePaths, targetDir, isCut);

                if (isCut && isInternalClipboard)
                {
                    Clipboard.Clear();
                    _isCutOperation = false;
                }
            }
        }

        private static void CopyOrMovePaths(IEnumerable<string> sourcePaths, string targetDir, bool isMove)
        {
            foreach (var sourcePath in sourcePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                if (File.Exists(sourcePath))
                {
                    string destPath = Path.Combine(targetDir, Path.GetFileName(sourcePath));
                    if (isMove)
                    {
                        if (!IsSamePath(sourcePath, destPath) && !PathExists(destPath))
                            File.Move(sourcePath, destPath);
                    }
                    else
                    {
                        File.Copy(sourcePath, GetAvailableCopyPath(destPath, isDirectory: false));
                    }
                }
                else if (Directory.Exists(sourcePath))
                {
                    string destPath = Path.Combine(targetDir, Path.GetFileName(sourcePath));
                    if (isMove)
                    {
                        if (!IsSamePath(sourcePath, destPath) && !IsSubPathOf(destPath, sourcePath) && !PathExists(destPath))
                            Directory.Move(sourcePath, destPath);
                    }
                    else
                    {
                        CopyDirectory(sourcePath, GetAvailableCopyPath(destPath, isDirectory: true));
                    }
                }
            }
        }

        private static void SetClipboardPaths(string[] paths, bool isCut)
        {
            Clipboard.SetDataObject(CreatePathDataObject(paths, isCut), copy: true);
        }

        private static DataObject CreatePathDataObject(string[] paths, bool isCut)
        {
            var dataObject = new DataObject();
            dataObject.SetData(ClipboardFormat, paths);
            dataObject.SetData(DataFormats.FileDrop, paths);

            var fileDropList = new StringCollection();
            fileDropList.AddRange(paths);
            dataObject.SetFileDropList(fileDropList);
            dataObject.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(isCut ? 2 : 5)));

            return dataObject;
        }

        private bool HasClipboardPaths()
        {
            return TryGetClipboardPaths(out _, out _, out _);
        }

        private bool TryGetClipboardPaths(out string[] paths, out bool isCut, out bool isInternalClipboard)
        {
            paths = Array.Empty<string>();
            isCut = false;
            isInternalClipboard = false;

            try
            {
                var dataObject = Clipboard.GetDataObject();
                if (dataObject == null)
                    return false;

                if (dataObject.GetDataPresent(ClipboardFormat))
                {
                    isInternalClipboard = true;
                    var data = dataObject.GetData(ClipboardFormat);
                    if (data is string singlePath)
                        paths = new[] { singlePath };
                    else if (data is string[] pathArray)
                        paths = pathArray;
                }
                else if (dataObject.GetDataPresent(DataFormats.FileDrop) && dataObject.GetData(DataFormats.FileDrop) is string[] fileDropPaths)
                {
                    paths = fileDropPaths;
                }
                else if (Clipboard.ContainsFileDropList())
                {
                    paths = Clipboard.GetFileDropList().Cast<string>().ToArray();
                }

                isCut = IsPreferredDropMove(dataObject) || (isInternalClipboard && _isCutOperation);
                paths = paths.Where(path => File.Exists(path) || Directory.Exists(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                return paths.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPreferredDropMove(IDataObject dataObject)
        {
            if (!dataObject.GetDataPresent("Preferred DropEffect"))
                return false;

            if (dataObject.GetData("Preferred DropEffect") is MemoryStream stream)
            {
                stream.Position = 0;
                Span<byte> bytes = stackalloc byte[4];
                if (stream.Read(bytes) == 4)
                    return (BitConverter.ToInt32(bytes) & 2) == 2;
            }

            return false;
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var files = Directory.GetFiles(sourceDir);
            var directories = Directory.GetDirectories(sourceDir);

            Directory.CreateDirectory(destinationDir);
            foreach (var file in files)
                File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)));
            foreach (var dir in directories)
                CopyDirectory(dir, Path.Combine(destinationDir, Path.GetFileName(dir)));
        }

        private static string GetAvailableCopyPath(string desiredPath, bool isDirectory)
        {
            if (!PathExists(desiredPath))
                return desiredPath;

            string? directory = Path.GetDirectoryName(desiredPath);
            if (string.IsNullOrEmpty(directory))
                return desiredPath;

            string baseName = isDirectory
                ? Path.GetFileName(desiredPath)
                : Path.GetFileNameWithoutExtension(desiredPath);
            string extension = isDirectory ? string.Empty : Path.GetExtension(desiredPath);
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = Path.GetFileName(desiredPath);
                extension = string.Empty;
            }

            for (int count = 1; ; count++)
            {
                string candidate = Path.Combine(directory, $"{baseName} - Copy ({count}){extension}");
                if (!PathExists(candidate))
                    return candidate;
            }
        }

        private static bool PathExists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        private static bool IsSamePath(string left, string right)
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSubPathOf(string candidatePath, string parentPath)
        {
            string candidate = Path.GetFullPath(candidatePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string parent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return candidate.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAncestorOf(SolutionNode possibleAncestor, SolutionNode node)
        {
            var parent = node.Parent;
            while (parent != null)
            {
                if (ReferenceEquals(parent, possibleAncestor))
                    return true;
                parent = parent.Parent;
            }
            return false;
        }

        #endregion
    }

}
