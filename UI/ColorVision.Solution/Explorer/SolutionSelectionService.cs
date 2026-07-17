namespace ColorVision.Solution.Explorer
{
    /// <summary>
    /// Owns the logical selection for the solution tree. TreeView's built-in
    /// selection remains a focus implementation detail and is not command state.
    /// </summary>
    internal sealed class SolutionSelectionService
    {
        private readonly List<SolutionNode> _selectedNodes = new();

        public IReadOnlyList<SolutionNode> SelectedNodes => _selectedNodes;

        public SolutionNode? AnchorNode { get; private set; }

        public event EventHandler? SelectionChanged;

        public void SelectSingle(SolutionNode node)
        {
            ArgumentNullException.ThrowIfNull(node);
            ReplaceSelection([node]);
            AnchorNode = node;
        }

        public void SelectMany(IEnumerable<SolutionNode> nodes, SolutionNode? anchorNode = null)
        {
            ArgumentNullException.ThrowIfNull(nodes);
            var replacement = nodes.Distinct().ToList();
            ReplaceSelection(replacement);
            AnchorNode = anchorNode != null && replacement.Contains(anchorNode)
                ? anchorNode
                : replacement.LastOrDefault();
        }

        public void Toggle(SolutionNode node)
        {
            ArgumentNullException.ThrowIfNull(node);
            if (_selectedNodes.Remove(node))
            {
                node.IsMultiSelected = false;
                if (ReferenceEquals(AnchorNode, node))
                    AnchorNode = _selectedNodes.LastOrDefault();
            }
            else
            {
                _selectedNodes.Add(node);
                node.IsMultiSelected = true;
                AnchorNode = node;
            }
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SelectRange(IReadOnlyList<SolutionNode> visibleNodes, SolutionNode targetNode, bool additive)
        {
            ArgumentNullException.ThrowIfNull(visibleNodes);
            ArgumentNullException.ThrowIfNull(targetNode);

            int anchorIndex = AnchorNode == null ? -1 : IndexOf(visibleNodes, AnchorNode);
            int targetIndex = IndexOf(visibleNodes, targetNode);
            if (anchorIndex < 0 || targetIndex < 0)
            {
                SelectSingle(targetNode);
                return;
            }

            int start = Math.Min(anchorIndex, targetIndex);
            int count = Math.Abs(anchorIndex - targetIndex) + 1;
            IEnumerable<SolutionNode> range = visibleNodes.Skip(start).Take(count);
            ReplaceSelection(additive ? _selectedNodes.Concat(range) : range);
        }

        public void PreserveOrSelectForContext(SolutionNode node)
        {
            ArgumentNullException.ThrowIfNull(node);
            if (!_selectedNodes.Contains(node))
                SelectSingle(node);
        }

        public void Clear()
        {
            if (_selectedNodes.Count == 0 && AnchorNode == null)
                return;

            foreach (SolutionNode node in _selectedNodes)
                node.IsMultiSelected = false;
            _selectedNodes.Clear();
            AnchorNode = null;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public IReadOnlyList<SolutionNode> GetTopLevelNodes(Func<SolutionNode, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            var candidates = _selectedNodes.Where(predicate).ToList();
            return candidates
                .Where(node => !candidates.Any(parent => !ReferenceEquals(parent, node) && IsAncestorOf(parent, node)))
                .ToList();
        }

        private void ReplaceSelection(IEnumerable<SolutionNode> nodes)
        {
            var replacement = nodes.Distinct().ToList();
            foreach (SolutionNode node in _selectedNodes.Except(replacement))
                node.IsMultiSelected = false;
            foreach (SolutionNode node in replacement)
                node.IsMultiSelected = true;

            _selectedNodes.Clear();
            _selectedNodes.AddRange(replacement);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private static int IndexOf(IReadOnlyList<SolutionNode> nodes, SolutionNode target)
        {
            for (int index = 0; index < nodes.Count; index++)
            {
                if (ReferenceEquals(nodes[index], target))
                    return index;
            }
            return -1;
        }

        private static bool IsAncestorOf(SolutionNode possibleAncestor, SolutionNode node)
        {
            SolutionNode? parent = node.Parent;
            while (parent != null)
            {
                if (ReferenceEquals(parent, possibleAncestor))
                    return true;
                parent = parent.Parent;
            }
            return false;
        }
    }

    internal static class SolutionCommandIds
    {
        public const string Cut = "Cut";
        public const string Copy = "Copy";
        public const string Paste = "Paste";
        public const string Delete = "Delete";
        public const string Rename = "ReName";
        public const string Refresh = "Refresh";

        public static bool SupportsMultipleSelection(string? commandId)
        {
            return commandId is Cut or Copy or Delete;
        }
    }
}
