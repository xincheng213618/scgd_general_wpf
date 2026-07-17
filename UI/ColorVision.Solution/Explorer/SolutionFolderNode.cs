using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Solution.Explorer
{
    /// <summary>
    /// A virtual solution folder. It organizes projects and other solution
    /// folders but never maps to, renames, or deletes a physical directory.
    /// </summary>
    public sealed class SolutionFolderNode : SolutionNode, ISolutionContainerNode
    {
        private readonly SolutionExplorer _solutionExplorer;

        public SolutionFolderDefinition Definition { get; private set; }
        public string FolderId => Definition.Id;
        internal SolutionExplorer SolutionExplorer => _solutionExplorer;

        public SolutionContainerAction SupportedContainerActions =>
            SolutionContainerAction.AddNewItem
            | SolutionContainerAction.AddExistingItem
            | SolutionContainerAction.AddNewProject
            | SolutionContainerAction.AddExistingProject
            | SolutionContainerAction.CreateSolutionFolder;

        public SolutionFolderNode(
            SolutionExplorer solutionExplorer,
            SolutionFolderDefinition definition)
        {
            _solutionExplorer = solutionExplorer;
            Definition = definition;
            Name1 = definition.Name;
            // Virtual folders deliberately have no filesystem path. This keeps
            // document-close, clipboard, and recycle-bin operations from ever
            // treating them as physical resources.
            FullPath = string.Empty;
            Icon = FileIcon.GetDirectoryIconImageSource();
            CanCopy = false;
            CanCut = false;
            CanReName = true;
            Initialize();
        }

        public override void InitMenuItem()
        {
            MenuItemMetadatas.Clear();
            IReadOnlyList<(string? Id, string DisplayName)> moveOptions =
                _solutionExplorer.GetSolutionFolderMoveOptions(FolderId);
            if (moveOptions.Count > 1)
            {
                const string moveMenuId = "MoveSolutionFolder";
                MenuItemMetadatas.Add(new MenuItemMetadata
                {
                    GuidId = moveMenuId,
                    Order = 40,
                    Header = "移动到解决方案文件夹(_M)",
                });
                string? currentParentId = _solutionExplorer.GetSolutionFolderParentId(FolderId);
                int order = 0;
                foreach (var option in moveOptions)
                {
                    string? targetFolderId = option.Id;
                    MenuItemMetadatas.Add(new MenuItemMetadata
                    {
                        OwnerGuid = moveMenuId,
                        GuidId = $"MoveSolutionFolder.{targetFolderId ?? "Root"}",
                        Order = order++,
                        Header = option.DisplayName,
                        IsChecked = string.Equals(
                            currentParentId,
                            targetFolderId,
                            StringComparison.OrdinalIgnoreCase),
                        Command = new RelayCommand(_ => MoveToSolutionFolder(targetFolderId)),
                    });
                }
            }
            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                GuidId = SolutionCommandIds.Delete,
                Order = 60,
                Header = "移除解决方案文件夹(_V)",
                Command = ApplicationCommands.Delete,
            });
        }

        public override bool ReName(string name)
        {
            if (!_solutionExplorer.TryRenameSolutionFolder(FolderId, name))
                return false;

            Definition.Name = name.Trim();
            return true;
        }

        public void ExecuteContainerAction(SolutionContainerAction action)
        {
            if (!CanAdd || !this.Supports(action))
                return;

            switch (action)
            {
                case SolutionContainerAction.AddNewItem:
                    _solutionExplorer.ShowAddNewItemDialog(FolderId);
                    break;
                case SolutionContainerAction.AddExistingItem:
                    _solutionExplorer.AddExistingItem(FolderId);
                    break;
                case SolutionContainerAction.AddNewProject:
                    _solutionExplorer.ShowAddNewProjectDialog(FolderId);
                    break;
                case SolutionContainerAction.AddExistingProject:
                    _solutionExplorer.AddExistingProject(FolderId);
                    break;
                case SolutionContainerAction.CreateSolutionFolder:
                    _solutionExplorer.CreateSolutionFolder(FolderId);
                    break;
            }
        }

        internal void UpdateDefinition(SolutionFolderDefinition definition)
        {
            if (!string.Equals(FolderId, definition.Id, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("不能使用不同 ID 的定义更新现有解决方案文件夹节点。");

            Definition = definition;
            Name1 = definition.Name;
            NotifyPropertyChanged(nameof(Name));
            InvalidateMenuItems();
        }

        internal override bool TryDelete(bool showConfirmation)
        {
            if (showConfirmation
                && MessageBox.Show(
                    Application.Current?.GetActiveWindow(),
                    $"从解决方案中移除文件夹“{Name}”吗？其中的项目和子文件夹将移动到上一级。",
                    "ColorVision",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question) != MessageBoxResult.OK)
            {
                return false;
            }

            return _solutionExplorer.RemoveSolutionFolder(FolderId);
        }

        private void MoveToSolutionFolder(string? targetFolderId)
        {
            if (_solutionExplorer.MoveSolutionItemsToFolder(
                [],
                [FolderId],
                targetFolderId,
                out string errorMessage))
            {
                return;
            }

            MessageBox.Show(
                Application.Current?.GetActiveWindow(),
                errorMessage,
                "ColorVision",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
