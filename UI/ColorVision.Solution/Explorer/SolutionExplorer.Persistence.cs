#pragma warning disable CS4014,CS8602,CS8604
using ColorVision.Common.MVVM;
using ColorVision.Solution.Properties;
using ColorVision.Solution.Workspace;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.Solution.Explorer
{
    public partial class SolutionExplorer
    {
        internal bool IsPathWithinSolution(string fullPath)
        {
            string relativePath = Path.GetRelativePath(DirectoryInfo.FullName, fullPath);
            return !Path.IsPathRooted(relativePath)
                && !string.Equals(relativePath, "..", StringComparison.Ordinal)
                && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
        }


        public override void Open()
        {
            new SolutionEditor().Open(FullPath);
        }

        public override void Refresh()
        {
            if (IsExplicitProjectMode)
            {
                _ = RefreshExplicitProjectStateWithFeedbackAsync(reloadSolutionState: true);
                return;
            }
            ReloadSolutionState();
        }

        internal IReadOnlyList<SolutionNode> DeleteNodesAsSingleOperation(IReadOnlyList<SolutionNode> nodes)
        {
            ArgumentNullException.ThrowIfNull(nodes);
            List<SolutionNode> distinctNodes = nodes.Distinct().ToList();
            if (distinctNodes.Count == 0)
                return Array.Empty<SolutionNode>();

            return ExecuteTrackedMutation(
                $"删除或移除 {distinctNodes.Count} 项",
                () => SolutionBatchDeleteService.Delete(distinctNodes),
                _ => true);
        }

        private TResult ExecuteTrackedMutation<TResult>(
            string description,
            Func<TResult> mutation,
            Func<TResult, bool> succeeded)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(description);
            ArgumentNullException.ThrowIfNull(mutation);
            ArgumentNullException.ThrowIfNull(succeeded);

            bool isOuterMutation = _trackedMutationDepth == 0;
            string? beforeSnapshot = isOuterMutation && _operationHistoryEnabled && !_isApplyingOperationSnapshot
                ? SolutionConfigStore.Serialize(Config)
                : null;
            _trackedMutationDepth++;
            try
            {
                TResult result = mutation();
                if (beforeSnapshot != null && succeeded(result))
                {
                    OperationHistory.Record(
                        description,
                        beforeSnapshot,
                        SolutionConfigStore.Serialize(Config));
                }
                return result;
            }
            finally
            {
                _trackedMutationDepth--;
            }
        }

        internal bool CanUndoSolutionOperation => OperationHistory.CanUndo;
        internal bool CanRedoSolutionOperation => OperationHistory.CanRedo;

        internal bool TryUndoSolutionOperation(out string errorMessage)
        {
            string applyError = string.Empty;
            bool applied = OperationHistory.TryUndo(snapshot =>
                TryApplyOperationSnapshot(snapshot, out applyError));
            errorMessage = applied
                ? string.Empty
                : string.IsNullOrWhiteSpace(applyError) ? "没有可撤销的解决方案操作。" : applyError;
            return applied;
        }

        internal bool TryRedoSolutionOperation(out string errorMessage)
        {
            string applyError = string.Empty;
            bool applied = OperationHistory.TryRedo(snapshot =>
                TryApplyOperationSnapshot(snapshot, out applyError));
            errorMessage = applied
                ? string.Empty
                : string.IsNullOrWhiteSpace(applyError) ? "没有可重做的解决方案操作。" : applyError;
            return applied;
        }

        private bool TryApplyOperationSnapshot(string snapshot, out string errorMessage)
        {
            string rollbackSnapshot = SolutionConfigStore.Serialize(Config);
            _isApplyingOperationSnapshot = true;
            try
            {
                ApplyOperationSnapshot(snapshot);
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or InvalidDataException
                or InvalidOperationException
                or ArgumentException
                or NotSupportedException)
            {
                Logger.Error("应用解决方案操作历史失败。", ex);
                try
                {
                    ApplyOperationSnapshot(rollbackSnapshot);
                }
                catch (Exception rollbackException)
                {
                    Logger.Error("回滚解决方案操作历史失败。", rollbackException);
                }
                errorMessage = $"无法应用解决方案操作历史：{ex.Message}";
                return false;
            }
            finally
            {
                _isApplyingOperationSnapshot = false;
            }
        }

        private void ApplyOperationSnapshot(string snapshot)
        {
            Config = SolutionConfigStore.DeserializeAndMigrate(snapshot, out _);
            SaveConfig();
            ReloadSolutionState();
            NotifyPropertyChanged(nameof(ActiveConfiguration));
            NotifyPropertyChanged(nameof(ActivePlatform));
            NotifyPropertyChanged(nameof(ActiveConfigurationDisplay));
        }

        /// <summary>
        /// 保存当前配置到文件
        /// </summary>
        public void SaveConfig()
        {
            if (Config == null)
                return;
            SolutionConfigStore.Save(ConfigFileInfo.FullName, Config);
        }

        private bool SaveConfigWithUserFeedback()
        {
            try
            {
                SaveConfig();
                return true;
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or InvalidDataException
                or ArgumentException
                or NotSupportedException)
            {
                Logger.Error($"保存解决方案失败: {ConfigFileInfo.FullName}", ex);
                MessageBox.Show(
                    Application.Current?.GetActiveWindow(),
                    $"无法保存解决方案配置。\n\n{ex.Message}",
                    "保存解决方案失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        private static void WriteTextAtomically(string filePath, string content)
        {
            string temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(temporaryPath, content);
                File.Move(temporaryPath, filePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private SolutionNode? FindNodeByFullPath(string fullPath)
        {
            foreach (var child in VisualChildren)
            {
                if (PathEquals(child.FullPath, fullPath))
                    return child;

                var found = FindNodeByFullPath(child, fullPath);
                if (found != null)
                    return found;
            }

            return null;
        }

        private List<SolutionNode> FindNodesByFullPath(string fullPath)
        {
            var result = new List<SolutionNode>();
            CollectNodesByFullPath(this, fullPath, result);
            return result;
        }

        private static void CollectNodesByFullPath(SolutionNode parent, string fullPath, List<SolutionNode> result)
        {
            foreach (SolutionNode child in parent.VisualChildren)
            {
                if (PathEquals(child.FullPath, fullPath))
                    result.Add(child);
                CollectNodesByFullPath(child, fullPath, result);
            }
        }

        private static SolutionNode? FindNodeByFullPath(SolutionNode node, string fullPath)
        {
            foreach (var child in node.VisualChildren)
            {
                if (PathEquals(child.FullPath, fullPath))
                    return child;

                var found = FindNodeByFullPath(child, fullPath);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static bool PathEquals(string? left, string? right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static void DisposeVisualChildren(SolutionNode parent)
        {
            foreach (SolutionNode child in parent.VisualChildren.ToList())
            {
                if (child is IDisposable disposable)
                    disposable.Dispose();
                else
                    DisposeVisualChildren(child);
            }
        }

        /// <summary>
        /// 显示文件夹属性
        /// </summary>
        public override void ShowProperty()
        {
            Common.NativeMethods.FileProperties.ShowFolderProperties(DirectoryInfo.FullName);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Disposing?.Invoke(this, EventArgs.Empty);
            ProjectProviderRegistry.ProvidersChanged -= ProjectProviderRegistry_ProvidersChanged;
            AppDomain.CurrentDomain.ProcessExit -= _processExitHandler;
            _fileSystemWatcher?.Dispose();
            _changedDebounceTimer?.Stop();
            _projectChangedDebounceTimer?.Stop();
            CancelActiveProjectRefresh();
            DisposeVisualChildren(this);
            VisualChildren.Clear();
            Cache?.Dispose();
            GC.SuppressFinalize(this);
        }

    }
}
