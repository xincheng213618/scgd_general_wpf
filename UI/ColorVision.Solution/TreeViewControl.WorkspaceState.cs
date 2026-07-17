#pragma warning disable CA1868
using ColorVision.Solution.Editor;
using ColorVision.Solution.Explorer;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.Solution
{
    public partial class TreeViewControl
    {
        private void ScheduleWorkspaceStateSave()
        {
            if (_isRestoringWorkspaceState || !IsLoaded)
                return;

            _workspaceStateSaveTimer.Stop();
            _workspaceStateSaveTimer.Start();
        }

        private void WorkspaceStateSaveTimer_Tick(object? sender, EventArgs e)
        {
            _workspaceStateSaveTimer.Stop();
            SaveWorkspaceState(SolutionManager.CurrentSolutionExplorer);
        }

        private void SaveWorkspaceState(SolutionExplorer? explorer)
        {
            if (_isRestoringWorkspaceState || explorer == null)
                return;

            try
            {
                SolutionWorkspaceState state = SolutionWorkspaceStateStore.Capture(
                    explorer,
                    _selectionService.CommandNodes,
                    _selectionService.AnchorNode?.ResolveCommandTarget());
                SolutionWorkspaceStateStore.Save(explorer.ConfigFileInfo.FullName, state);
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
            {
                Debug.WriteLine($"保存解决方案工作区状态失败: {ex.Message}");
            }
        }

        private async void RestoreWorkspaceState(SolutionExplorer? explorer)
        {
            if (explorer == null
                || !ReferenceEquals(explorer, SolutionManager.CurrentSolutionExplorer))
            {
                return;
            }

            CancelWorkspaceStateRestore();
            var cancellation = new CancellationTokenSource();
            _workspaceStateRestoreCancellation = cancellation;
            SolutionWorkspaceStateLoadResult loadResult = SolutionWorkspaceStateStore.Load(
                explorer.ConfigFileInfo.FullName);
            _isRestoringWorkspaceState = true;
            try
            {
                _selectionService.Clear();
                if (!loadResult.HasPersistedState)
                    return;

                await SolutionWorkspaceStateStore.RestoreExpansionAsync(
                    explorer,
                    loadResult.State,
                    cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(explorer, SolutionManager.CurrentSolutionExplorer))
                    return;
                IReadOnlyList<SolutionNode> selectedNodes = SolutionWorkspaceStateStore.ResolveSelectedNodes(
                    explorer,
                    loadResult.State);
                SolutionNode? anchorNode = SolutionWorkspaceStateStore.ResolveAnchorNode(
                    explorer,
                    loadResult.State);
                _selectionService.SelectMany(selectedNodes, anchorNode);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"恢复解决方案工作区状态失败: {ex}");
            }
            finally
            {
                if (ReferenceEquals(_workspaceStateRestoreCancellation, cancellation))
                {
                    _workspaceStateRestoreCancellation = null;
                    _isRestoringWorkspaceState = false;
                    cancellation.Dispose();
                }
            }
        }

        private void CancelWorkspaceStateRestore()
        {
            _workspaceStateRestoreCancellation?.Cancel();
            _workspaceStateRestoreCancellation?.Dispose();
            _workspaceStateRestoreCancellation = null;
            _isRestoringWorkspaceState = false;
        }
    }
}
