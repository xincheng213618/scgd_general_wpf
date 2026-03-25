using System.Collections.Generic;
using System.Windows.Media;

namespace ColorVision.Treemap
{
    /// <summary>
    /// Represents a node in the treemap hierarchy (e.g. a file or folder).
    /// </summary>
    public class TreemapNode
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The weight/size of this node.  For leaf nodes this is the file size
        /// (or any numeric value).  For container nodes it is typically the sum
        /// of its children's sizes and is computed automatically when children
        /// are added via <see cref="AddChild"/>.
        /// </summary>
        public double Size { get; set; }

        /// <summary>
        /// Optional user-visible label that overrides <see cref="Name"/> in tooltips.
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// Optional fill colour.  When null the control assigns a colour
        /// automatically based on depth / index.
        /// </summary>
        public Color? Color { get; set; }

        public List<TreemapNode> Children { get; } = new List<TreemapNode>();

        public bool IsLeaf => Children.Count == 0;

        public void AddChild(TreemapNode child)
        {
            Children.Add(child);
        }

        /// <summary>
        /// Recomputes <see cref="Size"/> from children recursively.
        /// Call this after building the tree if you did not set Size on
        /// non-leaf nodes manually.
        /// </summary>
        public void RecalculateSize()
        {
            if (IsLeaf) return;
            double total = 0;
            foreach (var child in Children)
            {
                child.RecalculateSize();
                total += child.Size;
            }
            Size = total;
        }

        public override string ToString() =>
            Label != null ? Label : Name;
    }
}
