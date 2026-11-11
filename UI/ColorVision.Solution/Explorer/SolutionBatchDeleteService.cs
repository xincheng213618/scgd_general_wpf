using ColorVision.Common.NativeMethods;
using log4net;
using System.IO;

namespace ColorVision.Solution.Explorer
{
    /// <summary>
    /// Executes one physical recycle-bin operation for a multi-selection while
    /// preserving the distinct removal semantics of virtual solution nodes.
    /// </summary>
    internal static class SolutionBatchDeleteService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SolutionBatchDeleteService));

        public static IReadOnlyList<SolutionNode> Delete(
            IReadOnlyList<SolutionNode> nodes,
            Func<string[], int>? recycle = null)
        {
            ArgumentNullException.ThrowIfNull(nodes);
            recycle ??= paths => ShellFileOperations.DeleteToRecycleBin(paths);

            List<SolutionNode> distinctNodes = nodes.Distinct().ToList();
            var failedNodes = distinctNodes
                .Where(node => !node.CanDelete)
                .ToHashSet();
            List<SolutionNode> deletableNodes = distinctNodes
                .Where(node => node.CanDelete)
                .ToList();
            List<(SolutionNode Node, string Path)> physicalNodes = deletableNodes
                .Select(node => (Node: node, Path: node.PhysicalDeletePath))
                .Where(item => !string.IsNullOrWhiteSpace(item.Path))
                .Select(item => (item.Node, Path.GetFullPath(item.Path!)))
                .ToList();
            string[] existingPaths = physicalNodes
                .Select(item => item.Path)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(path => File.Exists(path) || Directory.Exists(path))
                .ToArray();

            if (existingPaths.Length > 0)
            {
                try
                {
                    int result = recycle(existingPaths);
                    if (result != 0)
                        Log.Warn($"批量移到回收站时 Shell 返回代码 {result}。");
                }
                catch (Exception ex)
                {
                    Log.Warn("批量移到回收站失败。", ex);
                }
            }

            // The shell reports one result for the batch. Each node follows the actual state of its path.
            foreach ((SolutionNode node, string path) in physicalNodes)
            {
                if (!IsConfirmedMissing(path))
                {
                    failedNodes.Add(node);
                    continue;
                }
                node.FindSolutionExplorer()?.Cache?.Remove(path);
                if (!node.CompletePhysicalDelete())
                    failedNodes.Add(node);
            }

            HashSet<SolutionNode> physicalNodeSet = physicalNodes
                .Select(item => item.Node)
                .ToHashSet();
            foreach (SolutionNode node in deletableNodes.Where(node => !physicalNodeSet.Contains(node)))
            {
                if (!node.TryDelete(showConfirmation: false))
                    failedNodes.Add(node);
            }

            return distinctNodes.Where(failedNodes.Contains).ToList();
        }

        private static bool IsConfirmedMissing(string path)
        {
            try
            {
                File.GetAttributes(path);
                return false;
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn($"无法确认回收站操作后路径是否仍然存在：{path}", ex);
                return false;
            }
        }
    }
}
