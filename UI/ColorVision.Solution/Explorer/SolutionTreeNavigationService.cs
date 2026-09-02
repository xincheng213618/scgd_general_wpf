using System.IO;

namespace ColorVision.Solution.Explorer
{
    internal static class SolutionTreeNavigationService
    {
        public static void CollapseDescendants(SolutionNode rootNode)
        {
            ArgumentNullException.ThrowIfNull(rootNode);
            foreach (SolutionNode node in rootNode.VisualChildren.GetAllVisualChildren().ToList())
                node.IsExpanded = false;
            rootNode.IsExpanded = true;
        }

        public static bool CanResolvePath(SolutionNode rootNode, string fullPath)
        {
            ArgumentNullException.ThrowIfNull(rootNode);
            if (string.IsNullOrWhiteSpace(fullPath) || !Path.IsPathFullyQualified(fullPath))
                return false;
            if (EnumerateNodes(rootNode).Any(node => MatchesResourcePath(node, fullPath)))
                return true;
            if (rootNode is SolutionExplorer explorer)
            {
                return (!explorer.IsExplicitProjectMode && IsPathWithin(explorer.DirectoryInfo.FullName, fullPath))
                    || EnumerateNodes(explorer).OfType<ProjectNode>().Any(project =>
                        IsPathWithin(project.Project.ProjectDirectory.FullName, fullPath) && project.IncludesPath(fullPath));
            }
            return rootNode is FolderNode folder && IsPathWithin(folder.DirectoryInfo.FullName, fullPath);
        }

        public static async Task<SolutionNode?> ResolvePathAsync(
            SolutionNode rootNode,
            string fullPath,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(rootNode);
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanResolvePath(rootNode, fullPath))
                return null;

            SolutionNode? existingNode = EnumerateNodes(rootNode).FirstOrDefault(node => MatchesResourcePath(node, fullPath));
            if (existingNode != null)
                return existingNode;

            if (rootNode is SolutionExplorer explorer)
            {
                foreach (ProjectNode projectNode in EnumerateNodes(explorer)
                    .OfType<ProjectNode>()
                    .Where(project => IsPathWithin(project.Project.ProjectDirectory.FullName, fullPath)
                        && project.IncludesPath(fullPath))
                    .OrderByDescending(project => project.Project.ProjectDirectory.FullName.Length))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    SolutionNode? projectResult = await ResolvePhysicalPathAsync(
                        projectNode,
                        projectNode.Project.ProjectDirectory.FullName,
                        fullPath,
                        cancellationToken);
                    if (projectResult != null)
                        return projectResult;
                }

                return !explorer.IsExplicitProjectMode
                    ? await ResolvePhysicalPathAsync(explorer, explorer.DirectoryInfo.FullName, fullPath, cancellationToken)
                    : null;
            }

            return rootNode is FolderNode folder
                ? await ResolvePhysicalPathAsync(folder, folder.DirectoryInfo.FullName, fullPath, cancellationToken)
                : null;
        }

        public static async Task<SolutionNode?> ResolveNodeAsync(
            SolutionExplorer explorer,
            SolutionNode targetNode,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(explorer);
            ArgumentNullException.ThrowIfNull(targetNode);
            cancellationToken.ThrowIfCancellationRequested();

            if (ContainsReference(explorer, targetNode))
                return targetNode;

            string fullPath = targetNode.FullPath;
            if (string.IsNullOrWhiteSpace(fullPath))
                return ResolveByStableId(explorer, targetNode);

            ProjectNode? preferredProject = ProjectNode.FindOwningProject(targetNode);
            if (preferredProject != null && ContainsReference(explorer, preferredProject))
            {
                SolutionNode? projectResult = await ResolvePhysicalPathAsync(
                    preferredProject,
                    preferredProject.Project.ProjectDirectory.FullName,
                    fullPath,
                    cancellationToken);
                if (projectResult != null)
                    return projectResult;
            }

            SolutionNode? existingNode = ResolveByStableId(explorer, targetNode);
            if (existingNode != null)
                return existingNode;

            foreach (ProjectNode projectNode in EnumerateNodes(explorer)
                .OfType<ProjectNode>()
                .Where(project => IsPathWithin(project.Project.ProjectDirectory.FullName, fullPath)
                    && project.IncludesPath(fullPath))
                .OrderByDescending(project => project.Project.ProjectDirectory.FullName.Length))
            {
                cancellationToken.ThrowIfCancellationRequested();
                SolutionNode? projectResult = await ResolvePhysicalPathAsync(
                    projectNode,
                    projectNode.Project.ProjectDirectory.FullName,
                    fullPath,
                    cancellationToken);
                if (projectResult != null)
                    return projectResult;
            }

            if (!explorer.IsExplicitProjectMode && IsPathWithin(explorer.DirectoryInfo.FullName, fullPath))
            {
                return await ResolvePhysicalPathAsync(
                    explorer,
                    explorer.DirectoryInfo.FullName,
                    fullPath,
                    cancellationToken);
            }

            return null;
        }

        private static async Task<SolutionNode?> ResolvePhysicalPathAsync(
            SolutionNode rootNode,
            string rootPath,
            string targetPath,
            CancellationToken cancellationToken)
        {
            if (!IsPathWithin(rootPath, targetPath))
                return null;
            if (PathEquals(rootPath, targetPath))
                return rootNode;

            string relativePath;
            try
            {
                relativePath = Path.GetRelativePath(rootPath, targetPath);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }

            string[] segments = relativePath.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
            SolutionNode currentNode = rootNode;
            string currentPath = rootPath;
            foreach (string segment in segments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (currentNode is FolderNode folderNode)
                {
                    folderNode.IsExpanded = true;
                    await folderNode.EnsureChildrenLoadedAsync().WaitAsync(cancellationToken);
                }

                currentPath = Path.Combine(currentPath, segment);
                SolutionNode? childNode = currentNode.VisualChildren.FirstOrDefault(child =>
                    PathEquals(child.FullPath, currentPath));
                if (childNode == null)
                {
                    MaterializePhysicalChild(currentNode, currentPath);
                    childNode = currentNode.VisualChildren.FirstOrDefault(child =>
                        PathEquals(child.FullPath, currentPath));
                }
                if (childNode == null)
                    return null;

                currentNode = childNode;
            }
            return currentNode;
        }

        private static void MaterializePhysicalChild(SolutionNode parentNode, string fullPath)
        {
            if (Directory.Exists(fullPath))
                SolutionNodeFactory.AddFolderNode(parentNode, new DirectoryInfo(fullPath));
            else if (File.Exists(fullPath))
                SolutionNodeFactory.AddFileNode(parentNode, new FileInfo(fullPath));
        }

        private static SolutionNode? ResolveByStableId(
            SolutionExplorer explorer,
            SolutionNode targetNode)
        {
            string? targetId = SolutionWorkspaceStateStore.GetNodeId(targetNode);
            if (targetId == null)
                return null;
            return EnumerateNodes(explorer).FirstOrDefault(node => string.Equals(
                SolutionWorkspaceStateStore.GetNodeId(node),
                targetId,
                StringComparison.OrdinalIgnoreCase));
        }

        private static bool ContainsReference(SolutionExplorer explorer, SolutionNode targetNode)
        {
            return EnumerateNodes(explorer).Any(node => ReferenceEquals(node, targetNode));
        }

        private static IEnumerable<SolutionNode> EnumerateNodes(SolutionNode rootNode)
        {
            yield return rootNode;
            foreach (SolutionNode node in rootNode.VisualChildren.GetAllVisualChildren())
                yield return node;
        }

        private static bool MatchesResourcePath(SolutionNode node, string fullPath)
        {
            string? resourcePath = node switch
            {
                SolutionExplorer explorer => explorer.ConfigFileInfo.FullName,
                ProjectNode project => project.Project.ProjectFile.FullName,
                _ => node.EditorResourcePath,
            };
            return PathEquals(node.FullPath, fullPath) || PathEquals(resourcePath, fullPath);
        }

        private static bool IsPathWithin(string rootPath, string candidatePath)
        {
            try
            {
                string relativePath = Path.GetRelativePath(rootPath, candidatePath);
                return !Path.IsPathRooted(relativePath)
                    && !string.Equals(relativePath, "..", StringComparison.Ordinal)
                    && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        private static bool PathEquals(string? left, string? right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
