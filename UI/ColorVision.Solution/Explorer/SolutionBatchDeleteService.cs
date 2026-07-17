using ColorVision.Common.NativeMethods;
using System.IO;

namespace ColorVision.Solution.Explorer
{
    /// <summary>
    /// Executes one physical recycle-bin operation for a multi-selection while
    /// preserving the distinct removal semantics of virtual solution nodes.
    /// </summary>
    internal static class SolutionBatchDeleteService
    {
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
            List<(SolutionNode Node, string Path)> existingPhysicalNodes = physicalNodes
                .Where(item => File.Exists(item.Path) || Directory.Exists(item.Path))
                .ToList();

            if (existingPhysicalNodes.Count > 0)
            {
                int result;
                try
                {
                    result = recycle(existingPhysicalNodes
                        .Select(item => item.Path)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray());
                }
                catch
                {
                    result = -1;
                }

                if (result != 0)
                {
                    foreach ((SolutionNode node, _) in existingPhysicalNodes)
                        failedNodes.Add(node);
                }
            }

            foreach ((SolutionNode node, string path) in physicalNodes)
            {
                if (failedNodes.Contains(node))
                    continue;
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
    }
}
