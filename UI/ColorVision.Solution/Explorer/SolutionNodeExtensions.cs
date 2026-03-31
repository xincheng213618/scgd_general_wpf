namespace ColorVision.Solution.Explorer
{
    public static class SolutionNodeExtensions
    {
        public static T? GetAncestor<T>(this SolutionNode node) where T : SolutionNode
        {
            if (node is T t)
                return t;

            if (node.Parent == null)
                return null;

            return node.Parent.GetAncestor<T>();
        }

        public static bool HasFile(this SolutionNode node)
        {
            for (int i = 0; i < node.VisualChildren.Count; i++)
            {
                if (node.VisualChildren[i] is FileNode)
                {
                    return true;
                }
                if (node.VisualChildren[i] is FolderNode folder)
                {
                    if (folder.HasFile()) return true;
                }
            }
            return false;
        }

        public static void SortByName(this SolutionNode node)
        {
            var sorted = node.VisualChildren.OrderBy(v => v.Name).ToList();

            bool needsSort = false;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (!ReferenceEquals(node.VisualChildren[i], sorted[i]))
                {
                    needsSort = true;
                    break;
                }
            }

            if (!needsSort) return;

            for (int i = 0; i < sorted.Count; i++)
            {
                int currentIndex = node.VisualChildren.IndexOf(sorted[i]);
                if (currentIndex != i)
                {
                    node.VisualChildren.Move(currentIndex, i);
                }
            }
        }

        public static bool SetSelected(this SolutionNode node, string fullpath)
        {
            if (node.FullPath == fullpath)
            {
                node.IsSelected = true;
                var parent = node.Parent;
                while (parent != null)
                {
                    parent.IsExpanded = true;
                    parent = parent.Parent;
                }
                return true;
            }
            foreach (var child in node.VisualChildren)
            {
                if (SetSelected(child, fullpath))
                {
                    node.IsExpanded = true;
                    return true;
                }
            }
            return false;
        }

        public static IEnumerable<SolutionNode> GetAllVisualChildren(this IEnumerable<SolutionNode> visualChildren)
        {
            foreach (var child in visualChildren)
            {
                yield return child;

                foreach (var grandChild in GetAllVisualChildren(child.VisualChildren))
                {
                    yield return grandChild;
                }
            }
        }
    }
}
