using Newtonsoft.Json;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Solution.Explorer
{
    internal sealed class SolutionWorkspaceState
    {
        public int SchemaVersion { get; set; } = SolutionWorkspaceStateStore.CurrentSchemaVersion;
        public List<string> ExpandedNodeIds { get; set; } = new();
        public List<string> SelectedNodeIds { get; set; } = new();
        public string? AnchorNodeId { get; set; }
    }

    internal sealed record SolutionWorkspaceStateLoadResult(
        SolutionWorkspaceState State,
        bool HasPersistedState);

    /// <summary>
    /// Persists machine-local tree presentation state outside the shared
    /// workspace. Node ids are stable across incremental tree reconstruction.
    /// </summary>
    internal static class SolutionWorkspaceStateStore
    {
        public const int CurrentSchemaVersion = 1;

        public static SolutionWorkspaceStateLoadResult Load(
            string solutionPath,
            string? stateRootPath = null)
        {
            string statePath = GetStateFilePath(solutionPath, stateRootPath);
            if (!File.Exists(statePath))
                return new SolutionWorkspaceStateLoadResult(new SolutionWorkspaceState(), false);

            try
            {
                SolutionWorkspaceState? state = JsonConvert.DeserializeObject<SolutionWorkspaceState>(
                    File.ReadAllText(statePath));
                if (state == null || state.SchemaVersion > CurrentSchemaVersion || state.SchemaVersion < 1)
                    return new SolutionWorkspaceStateLoadResult(new SolutionWorkspaceState(), false);
                Normalize(state);
                return new SolutionWorkspaceStateLoadResult(state, true);
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or ArgumentException
                or NotSupportedException)
            {
                return new SolutionWorkspaceStateLoadResult(new SolutionWorkspaceState(), false);
            }
        }

        public static void Save(
            string solutionPath,
            SolutionWorkspaceState state,
            string? stateRootPath = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
            ArgumentNullException.ThrowIfNull(state);
            Normalize(state);

            string statePath = GetStateFilePath(solutionPath, stateRootPath);
            string directoryPath = Path.GetDirectoryName(statePath)!;
            Directory.CreateDirectory(directoryPath);
            string temporaryPath = $"{statePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(
                    temporaryPath,
                    JsonConvert.SerializeObject(state, Formatting.Indented),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                File.Move(temporaryPath, statePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        public static string GetStateFilePath(string solutionPath, string? stateRootPath = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
            string rootPath = string.IsNullOrWhiteSpace(stateRootPath)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ColorVision",
                    "SolutionState")
                : Path.GetFullPath(stateRootPath);
            string normalizedSolutionPath = Path.GetFullPath(solutionPath)
                .Replace('\\', '/')
                .ToUpperInvariant();
            string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedSolutionPath)));
            return Path.Combine(rootPath, $"{key}.json");
        }

        public static SolutionWorkspaceState Capture(
            SolutionExplorer explorer,
            IEnumerable<SolutionNode> selectedNodes,
            SolutionNode? anchorNode)
        {
            ArgumentNullException.ThrowIfNull(explorer);
            ArgumentNullException.ThrowIfNull(selectedNodes);
            var state = new SolutionWorkspaceState
            {
                ExpandedNodeIds = EnumerateNodes(explorer)
                    .Where(node => node.IsExpanded)
                    .Select(GetNodeId)
                    .Where(id => id != null)
                    .Cast<string>()
                    .ToList(),
                SelectedNodeIds = selectedNodes
                    .Select(GetNodeId)
                    .Where(id => id != null)
                    .Cast<string>()
                    .ToList(),
                AnchorNodeId = anchorNode == null ? null : GetNodeId(anchorNode),
            };
            Normalize(state);
            return state;
        }

        public static void RestoreExpansion(SolutionExplorer explorer, SolutionWorkspaceState state)
        {
            ArgumentNullException.ThrowIfNull(explorer);
            ArgumentNullException.ThrowIfNull(state);
            var expandedIds = state.ExpandedNodeIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (SolutionNode node in EnumerateNodes(explorer))
            {
                string? nodeId = GetNodeId(node);
                if (nodeId != null && node is not SolutionExplorer)
                    node.IsExpanded = expandedIds.Contains(nodeId);
            }
        }

        public static IReadOnlyList<SolutionNode> ResolveSelectedNodes(
            SolutionExplorer explorer,
            SolutionWorkspaceState state)
        {
            Dictionary<string, SolutionNode> nodesById = CreateNodeMap(explorer);
            return state.SelectedNodeIds
                .Where(nodesById.ContainsKey)
                .Select(id => nodesById[id])
                .Distinct()
                .ToList();
        }

        public static SolutionNode? ResolveAnchorNode(
            SolutionExplorer explorer,
            SolutionWorkspaceState state)
        {
            if (string.IsNullOrWhiteSpace(state.AnchorNodeId))
                return null;
            return CreateNodeMap(explorer).GetValueOrDefault(state.AnchorNodeId);
        }

        internal static string? GetNodeId(SolutionNode node)
        {
            return node switch
            {
                SolutionExplorer => "solution",
                SolutionFolderNode folderNode => $"solution-folder:{folderNode.FolderId}",
                SolutionItemNode itemNode => $"solution-item:{itemNode.ItemId}",
                ProjectNode projectNode => $"project:{NormalizePath(projectNode.Project.ProjectFile.FullName)}",
                UnavailableProjectNode unavailableNode => $"unavailable-project:{unavailableNode.ProjectReference.Trim().ToUpperInvariant()}",
                LazyLoadingNode => null,
                _ when !string.IsNullOrWhiteSpace(node.FullPath) => $"path:{NormalizePath(node.FullPath)}",
                _ => null,
            };
        }

        private static Dictionary<string, SolutionNode> CreateNodeMap(SolutionExplorer explorer)
        {
            return EnumerateNodes(explorer)
                .Select(node => (Id: GetNodeId(node), Node: node))
                .Where(item => item.Id != null)
                .GroupBy(item => item.Id!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Node, StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<SolutionNode> EnumerateNodes(SolutionExplorer explorer)
        {
            yield return explorer;
            foreach (SolutionNode node in explorer.VisualChildren.GetAllVisualChildren())
                yield return node;
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path).Replace('\\', '/').ToUpperInvariant();
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return path.Replace('\\', '/').ToUpperInvariant();
            }
        }

        private static void Normalize(SolutionWorkspaceState state)
        {
            state.SchemaVersion = CurrentSchemaVersion;
            state.ExpandedNodeIds = NormalizeIds(state.ExpandedNodeIds);
            state.SelectedNodeIds = NormalizeIds(state.SelectedNodeIds);
            state.AnchorNodeId = string.IsNullOrWhiteSpace(state.AnchorNodeId)
                ? null
                : state.AnchorNodeId.Trim();
        }

        private static List<string> NormalizeIds(IEnumerable<string>? ids)
        {
            return (ids ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
