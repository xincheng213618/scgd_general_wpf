using System.IO;
using System.Text;

namespace ColorVision.Solution.Explorer
{
    internal sealed record SolutionSearchHit(
        SolutionExplorer Explorer,
        string FullPath,
        string Name,
        bool IsDirectory,
        SolutionNode? ExistingNode,
        SolutionNode ParentNode,
        string DisplayPath,
        int MatchRank);

    internal sealed record SolutionSearchResult(
        IReadOnlyList<SolutionSearchHit> Hits,
        bool IsTruncated);

    internal static class SolutionSearchService
    {
        public const int DefaultMaxResults = 500;

        public static Task<SolutionSearchResult> SearchAsync(
            IReadOnlyList<SolutionExplorer> explorers,
            string query,
            int maxResults = DefaultMaxResults,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(explorers);
            ArgumentOutOfRangeException.ThrowIfLessThan(maxResults, 1);
            IReadOnlyList<string> keywords = ParseKeywords(query);
            if (keywords.Count == 0 || explorers.Count == 0)
                return Task.FromResult(new SolutionSearchResult([], IsTruncated: false));
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<SolutionSearchResult>(cancellationToken);

            List<SearchContext> contexts = explorers
                .Distinct()
                .Select(CreateContext)
                .ToList();
            return Task.Run(
                () => Search(contexts, keywords, query.Trim(), maxResults, cancellationToken),
                cancellationToken);
        }

        internal static Task<SolutionSearchResult> SearchFileSystemAsync(
            SolutionExplorer explorer,
            FileSystemFolderNode root,
            string query,
            int maxResults,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(explorer);
            ArgumentNullException.ThrowIfNull(root);
            ArgumentOutOfRangeException.ThrowIfLessThan(maxResults, 1);
            IReadOnlyList<string> keywords = ParseKeywords(query);
            if (keywords.Count == 0)
                return Task.FromResult(new SolutionSearchResult([], IsTruncated: false));
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<SolutionSearchResult>(cancellationToken);

            List<LoadedCandidate> loadedCandidates = new[] { root }
                .Concat(root.VisualChildren.GetAllVisualChildren())
                .Where(node => node is not LazyLoadingNode and not SolutionSearchResultNode)
                .Select(node => new LoadedCandidate(
                    node,
                    node.FullPath,
                    node.Name,
                    IsDirectoryNode(node),
                    node.Parent ?? root,
                    LogicalDisplayPath: string.Empty))
                .ToList();
            var context = new SearchContext(
                explorer,
                root.FullPath,
                explorer.Name,
                IsExplicitProjectMode: false,
                Cache: null,
                ProjectScopes: [],
                loadedCandidates,
                IndexLoadedCandidates(loadedCandidates),
                root,
                IsFileSystemView: true);
            return Task.Run(
                () => Search([context], keywords, query.Trim(), maxResults, cancellationToken),
                cancellationToken);
        }

        internal static IReadOnlyList<string> ParseKeywords(string? query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Array.Empty<string>();

            var keywords = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;
            foreach (char character in query)
            {
                if (character == '"')
                {
                    quoted = !quoted;
                    continue;
                }
                if (char.IsWhiteSpace(character) && !quoted)
                {
                    AddKeyword(keywords, current);
                    continue;
                }
                current.Append(character);
            }
            AddKeyword(keywords, current);
            return keywords.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static SolutionSearchResult Search(
            IReadOnlyList<SearchContext> contexts,
            IReadOnlyList<string> keywords,
            string query,
            int maxResults,
            CancellationToken cancellationToken)
        {
            int candidateLimit = maxResults > (int.MaxValue - 1) / 4
                ? int.MaxValue
                : maxResults * 4 + 1;
            var contextAccumulators = new List<SearchAccumulator>(contexts.Count);
            foreach (SearchContext context in contexts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var contextAccumulator = new SearchAccumulator(candidateLimit);
                foreach (LoadedCandidate candidate in context.LoadedCandidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string displayPath = ResolveLoadedDisplayPath(context, candidate);
                    if (!Matches(candidate.Name, candidate.FullPath, displayPath, keywords))
                        continue;
                    contextAccumulator.Add(new SolutionSearchHit(
                        context.Explorer,
                        candidate.FullPath,
                        candidate.Name,
                        candidate.IsDirectory,
                        candidate.Node,
                        candidate.ParentNode,
                        displayPath,
                        GetMatchRank(candidate.Name, displayPath, query, keywords)));
                }

                bool hasCache = context.Cache?.HasCache() == true;
                if (hasCache)
                    AddCachedCandidates(context, keywords, query, contextAccumulator, cancellationToken);

                IEnumerable<string> scanRoots = hasCache
                    ? context.ProjectScopes
                        .Select(scope => scope.RootPath)
                        .Where(rootPath => !IsPathWithin(context.RootPath, rootPath))
                    : context.IsExplicitProjectMode
                        ? context.ProjectScopes.Select(scope => scope.RootPath)
                        : [context.RootPath];
                foreach (string rootPath in scanRoots.Distinct(StringComparer.OrdinalIgnoreCase))
                    ScanDirectory(context, rootPath, keywords, query, contextAccumulator, cancellationToken);
                contextAccumulators.Add(contextAccumulator);
            }

            var mergedAccumulator = new SearchAccumulator(int.MaxValue);
            foreach (SearchAccumulator contextAccumulator in contextAccumulators)
                mergedAccumulator.AddRange(contextAccumulator.Hits);
            List<SolutionSearchHit> allHits = mergedAccumulator.Hits.ToList();
            List<SolutionSearchHit> orderedHits = allHits
                .Take(maxResults)
                .ToList();
            return new SolutionSearchResult(
                orderedHits,
                contextAccumulators.Any(accumulator => accumulator.WasTruncated)
                    || allHits.Count > maxResults);
        }

        private static void AddCachedCandidates(
            SearchContext context,
            IReadOnlyList<string> keywords,
            string query,
            SearchAccumulator accumulator,
            CancellationToken cancellationToken)
        {
            Predicate<string>? includesPath = context.ProjectScopes.Any(scope => scope.Node.Project.ItemRules != null)
                ? path => IsVisiblePhysicalPath(context, path, FindProjectScope(context.ProjectScopes, path))
                : null;
            IEnumerable<string> searchRoots = context.IsExplicitProjectMode
                ? context.ProjectScopes
                    .Select(scope => scope.RootPath)
                    .Where(rootPath => IsPathWithin(context.RootPath, rootPath))
                : [context.RootPath];
            foreach (string rootPath in searchRoots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                int queryLimit = accumulator.Capacity == int.MaxValue
                    ? int.MaxValue
                    : accumulator.Capacity + 1;
                List<FileTreeCacheEntry> entries = context.Cache!.Search(
                    keywords.ToArray(),
                    queryLimit,
                    rootPath,
                    cancellationToken,
                    nameQuery: query,
                    includesPath: includesPath);
                if (entries.Count > accumulator.Capacity)
                    accumulator.MarkTruncated();
                foreach (FileTreeCacheEntry entry in entries.Take(accumulator.Capacity))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AddPhysicalCandidate(
                        context,
                        entry.FullPath,
                        entry.Name,
                        entry.IsDirectory,
                        keywords,
                        query,
                        accumulator);
                }
            }
        }

        private static void ScanDirectory(
            SearchContext context,
            string rootPath,
            IReadOnlyList<string> keywords,
            string query,
            SearchAccumulator accumulator,
            CancellationToken cancellationToken)
        {
            if (!Directory.Exists(rootPath))
                return;

            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(rootPath);
            var enumerationOptions = new EnumerationOptions
            {
                AttributesToSkip = context.IsFileSystemView ? 0 : FileAttributes.Hidden | FileAttributes.System,
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false,
            };
            while (pendingDirectories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var directory = new DirectoryInfo(pendingDirectories.Pop());
                    foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos("*", enumerationOptions))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        bool isDirectory = entry is DirectoryInfo;
                        if (isDirectory && (entry.Attributes & FileAttributes.ReparsePoint) == 0)
                            pendingDirectories.Push(entry.FullName);
                        if (!isDirectory && !context.IsFileSystemView && SolutionNodeFactory.IsInternalFile(entry.Name))
                            continue;
                        AddPhysicalCandidate(
                            context,
                            entry.FullName,
                            entry.Name,
                            isDirectory,
                            keywords,
                            query,
                            accumulator);
                    }
                }
                catch (Exception ex) when (ex is IOException
                    or UnauthorizedAccessException
                    or ArgumentException
                    or NotSupportedException)
                {
                }
            }
        }

        private static void AddPhysicalCandidate(
            SearchContext context,
            string fullPath,
            string name,
            bool isDirectory,
            IReadOnlyList<string> keywords,
            string query,
            SearchAccumulator accumulator)
        {
            if (!Matches(name, fullPath, fullPath, keywords))
                return;

            ProjectScope? projectScope = FindProjectScope(context.ProjectScopes, fullPath);
            if (!IsVisiblePhysicalPath(context, fullPath, projectScope))
                return;

            context.LoadedNodesByPath.TryGetValue(fullPath, out LoadedCandidate? loadedCandidate);
            SolutionNode? existingNode = loadedCandidate?.Node;
            SolutionNode parentNode = existingNode?.Parent
                ?? (SolutionNode?)projectScope?.Node
                ?? context.RootNode;
            string displayPath = loadedCandidate == null
                ? CreatePhysicalDisplayPath(context.SolutionName, context.RootPath, projectScope, fullPath)
                : ResolveLoadedDisplayPath(context, loadedCandidate);
            string displayName = existingNode?.Name ?? name;
            accumulator.Add(new SolutionSearchHit(
                context.Explorer,
                fullPath,
                displayName,
                existingNode != null ? IsDirectoryNode(existingNode) : isDirectory,
                existingNode,
                parentNode,
                displayPath,
                GetMatchRank(displayName, displayPath, query, keywords)));
        }

        private static bool IsVisiblePhysicalPath(SearchContext context, string fullPath, ProjectScope? projectScope)
        {
            return projectScope != null
                ? projectScope.Node.IncludesPath(fullPath)
                : !context.IsExplicitProjectMode && IsPathWithin(context.RootPath, fullPath);
        }

        private static SearchContext CreateContext(SolutionExplorer explorer)
        {
            string solutionName = explorer.Name;
            var nodes = new[] { explorer }
                .Concat(explorer.VisualChildren.GetAllVisualChildren())
                .Where(node => node is not LazyLoadingNode and not SolutionSearchResultNode)
                .ToList();
            List<ProjectScope> projectScopes = nodes
                .OfType<ProjectNode>()
                .Select(node => new ProjectScope(node, node.Project.ProjectDirectory.FullName, node.Name))
                .GroupBy(scope => scope.RootPath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            var loadedCandidates = nodes
                .Select(node => new LoadedCandidate(
                    node,
                    node.FullPath,
                    node.Name,
                    IsDirectoryNode(node),
                    node.Parent ?? explorer,
                    CreateLogicalDisplayPath(solutionName, node)))
                .ToList();
            return new SearchContext(
                explorer,
                explorer.DirectoryInfo.FullName,
                solutionName,
                explorer.IsExplicitProjectMode,
                explorer.Cache,
                projectScopes,
                loadedCandidates,
                IndexLoadedCandidates(loadedCandidates),
                explorer,
                IsFileSystemView: false);
        }

        private static Dictionary<string, LoadedCandidate> IndexLoadedCandidates(IReadOnlyList<LoadedCandidate> candidates)
        {
            var index = new Dictionary<string, LoadedCandidate>(candidates.Count, StringComparer.OrdinalIgnoreCase);
            foreach (LoadedCandidate candidate in candidates)
                if (!string.IsNullOrWhiteSpace(candidate.FullPath)) index.TryAdd(candidate.FullPath, candidate);
            return index;
        }

        private static string ResolveLoadedDisplayPath(SearchContext context, LoadedCandidate candidate)
        {
            if (context.IsFileSystemView)
                return Path.GetRelativePath(context.RootPath, candidate.FullPath);

            string logicalPath = candidate.LogicalDisplayPath;
            if (string.IsNullOrWhiteSpace(candidate.FullPath))
                return logicalPath;

            string physicalPath = CreatePhysicalDisplayPath(context.SolutionName, context.RootPath,
                FindProjectScope(context.ProjectScopes, candidate.FullPath), candidate.FullPath);
            if (string.Equals(physicalPath, logicalPath, StringComparison.Ordinal))
                return logicalPath;

            return File.Exists(candidate.FullPath) || Directory.Exists(candidate.FullPath) ? physicalPath : logicalPath;
        }

        private static string CreateLogicalDisplayPath(string solutionName, SolutionNode node)
        {
            if (node is SolutionExplorer)
                return solutionName;
            if (node.Parent is null or SolutionExplorer)
                return string.IsNullOrWhiteSpace(node.Name) ? solutionName : node.Name;

            var names = new Stack<string>();
            for (SolutionNode? current = node; current != null && current is not SolutionExplorer; current = current.Parent)
            {
                if (!string.IsNullOrWhiteSpace(current.Name))
                    names.Push(current.Name);
            }
            return names.Count == 0 ? solutionName : Path.Combine(names.ToArray());
        }

        private static string CreatePhysicalDisplayPath(
            string solutionName,
            string solutionRootPath,
            ProjectScope? projectScope,
            string fullPath)
        {
            string rootPath = projectScope?.RootPath ?? solutionRootPath;
            string relativePath;
            try
            {
                relativePath = Path.GetRelativePath(rootPath, fullPath);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                relativePath = fullPath;
            }

            if (projectScope == null)
                return string.Equals(relativePath, ".", StringComparison.Ordinal) ? solutionName : relativePath;
            return string.Equals(relativePath, ".", StringComparison.Ordinal)
                ? projectScope.Name
                : Path.Combine(projectScope.Name, relativePath);
        }

        private static ProjectScope? FindProjectScope(
            IReadOnlyList<ProjectScope> scopes,
            string fullPath)
        {
            ProjectScope? best = null;
            foreach (ProjectScope scope in scopes)
                if ((best == null || scope.RootPath.Length > best.RootPath.Length) && IsPathWithin(scope.RootPath, fullPath))
                    best = scope;
            return best;
        }

        private static bool Matches(
            string name,
            string fullPath,
            string displayPath,
            IReadOnlyList<string> keywords)
        {
            return keywords.All(keyword =>
                name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || fullPath.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || displayPath.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private static int GetMatchRank(
            string name,
            string displayPath,
            string query,
            IReadOnlyList<string> keywords)
        {
            if (string.Equals(name, query, StringComparison.OrdinalIgnoreCase))
                return 0;
            if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                return 1;
            if (keywords.All(keyword => name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                return 2;
            return displayPath.Contains(query, StringComparison.OrdinalIgnoreCase) ? 3 : 4;
        }

        private static bool IsDirectoryNode(SolutionNode node)
        {
            return node is FolderNode or SolutionExplorer or SolutionFolderNode;
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

        private static void AddKeyword(List<string> keywords, StringBuilder current)
        {
            if (current.Length == 0)
                return;
            string keyword = current.ToString().Trim();
            current.Clear();
            if (keyword.Length > 0)
                keywords.Add(keyword);
        }

        private sealed record ProjectScope(ProjectNode Node, string RootPath, string Name);

        private sealed record LoadedCandidate(
            SolutionNode Node,
            string FullPath,
            string Name,
            bool IsDirectory,
            SolutionNode ParentNode,
            string LogicalDisplayPath);

        private sealed record SearchContext(
            SolutionExplorer Explorer,
            string RootPath,
            string SolutionName,
            bool IsExplicitProjectMode,
            SolutionCache? Cache,
            IReadOnlyList<ProjectScope> ProjectScopes,
            IReadOnlyList<LoadedCandidate> LoadedCandidates,
            IReadOnlyDictionary<string, LoadedCandidate> LoadedNodesByPath,
            SolutionNode RootNode,
            bool IsFileSystemView);

        private sealed class SearchAccumulator
        {
            private readonly Dictionary<string, SolutionSearchHit> _hits = new(StringComparer.OrdinalIgnoreCase);
            private readonly SortedSet<SolutionSearchHit> _orderedHits = new(Comparer<SolutionSearchHit>.Create(CompareHits));

            public int Capacity { get; }
            public bool WasTruncated { get; private set; }
            public IEnumerable<SolutionSearchHit> Hits => _orderedHits;

            public SearchAccumulator(int capacity)
            {
                Capacity = capacity;
            }

            public void Add(SolutionSearchHit hit)
            {
                string key = GetKey(hit);
                if (_hits.TryGetValue(key, out SolutionSearchHit? existing))
                {
                    if (existing.ExistingNode == null && hit.ExistingNode != null)
                    {
                        _orderedHits.Remove(existing);
                        _hits[key] = hit;
                        _orderedHits.Add(hit);
                    }
                    return;
                }
                if (_hits.Count >= Capacity)
                {
                    WasTruncated = true;
                    SolutionSearchHit? worstHit = _orderedHits.Max;
                    if (worstHit == null || CompareHits(hit, worstHit) >= 0)
                        return;
                    _orderedHits.Remove(worstHit);
                    _hits.Remove(GetKey(worstHit));
                }
                _hits.Add(key, hit);
                _orderedHits.Add(hit);
            }

            public void AddRange(IEnumerable<SolutionSearchHit> hits)
            {
                foreach (SolutionSearchHit hit in hits)
                    Add(hit);
            }

            public void MarkTruncated()
            {
                WasTruncated = true;
            }

            private static int CompareHits(SolutionSearchHit? left, SolutionSearchHit? right)
            {
                if (ReferenceEquals(left, right))
                    return 0;
                if (left == null)
                    return 1;
                if (right == null)
                    return -1;

                int result = left.MatchRank.CompareTo(right.MatchRank);
                if (result != 0)
                    return result;
                result = right.IsDirectory.CompareTo(left.IsDirectory);
                if (result != 0)
                    return result;
                result = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
                if (result != 0)
                    return result;
                result = StringComparer.OrdinalIgnoreCase.Compare(left.DisplayPath, right.DisplayPath);
                return result != 0
                    ? result
                    : StringComparer.OrdinalIgnoreCase.Compare(GetKey(left), GetKey(right));
            }

            private static string GetKey(SolutionSearchHit hit)
            {
                string identity = !string.IsNullOrWhiteSpace(hit.FullPath)
                    ? hit.FullPath
                    : hit.ExistingNode == null
                        ? $"{hit.Name}|{hit.DisplayPath}"
                        : SolutionWorkspaceStateStore.GetNodeId(hit.ExistingNode)
                            ?? $"{hit.Name}|{hit.DisplayPath}";
                return $"{hit.Explorer.ConfigFileInfo.FullName}|{identity}";
            }
        }
    }
}
