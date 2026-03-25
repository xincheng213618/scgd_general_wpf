using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.UI.Controls
{
    /// <summary>
    /// Calculates squarified treemap layout.
    /// Each node receives a <see cref="Rect"/> stored in <see cref="LayoutResult"/>.
    /// <see cref="RenderOrder"/> exposes the same data in parent-before-child order,
    /// which is required for correct z-order when rendering.
    /// </summary>
    public class TreemapLayout
    {
        /// <summary>Minimum side length (pixels) below which a node is not rendered.</summary>
        private const double MinNodeSide = 2.0;

        /// <summary>
        /// Height (pixels) reserved at the top of a folder node for its header band.
        /// Only applied when the node is tall enough to show the header and still
        /// leave room for children.
        /// </summary>
        public const double FolderHeaderHeight = 15.0;

        /// <summary>Uniform inset applied to the non-header sides of a folder node.</summary>
        public const double Inset = 2.0;

        // ─── Results ──────────────────────────────────────────────────────────

        /// <summary>Map from node to its layout rect (for O(1) hit-test lookup).</summary>
        public Dictionary<TreemapNode, Rect> LayoutResult { get; } =
            new Dictionary<TreemapNode, Rect>();

        /// <summary>
        /// All laid-out nodes in parent-before-child insertion order.
        /// Use this list when rendering so that children are drawn on top of parents.
        /// </summary>
        public List<(TreemapNode node, Rect rect)> RenderOrder { get; } =
            new List<(TreemapNode, Rect)>();

        // ─── Public API ───────────────────────────────────────────────────────

        public void Calculate(TreemapNode root, Rect bounds)
        {
            LayoutResult.Clear();
            RenderOrder.Clear();
            if (root == null || root.Size <= 0) return;

            if (root.IsLeaf)
            {
                LayoutResult[root] = bounds;
                RenderOrder.Add((root, bounds));
            }
            else
            {
                LayoutChildren(root.Children, root.Size, bounds);
            }
        }

        // ─── Internal layout ──────────────────────────────────────────────────

        private void LayoutChildren(List<TreemapNode> nodes, double parentSize, Rect bounds)
        {
            if (nodes.Count == 0 || parentSize <= 0) return;
            if (bounds.Width < MinNodeSide || bounds.Height < MinNodeSide) return;

            double totalArea = bounds.Width * bounds.Height;

            var items = new List<(TreemapNode node, double area)>();
            foreach (var n in nodes)
            {
                if (n.Size <= 0) continue;
                double area = (n.Size / parentSize) * totalArea;
                if (area < MinNodeSide * MinNodeSide) continue;
                items.Add((n, area));
            }

            if (items.Count == 0) return;

            // Sort largest-first so the squarified layout places the biggest nodes
            // in the top-left, producing the organised look of WizTree / similar tools.
            items.Sort(static (a, b) => b.area.CompareTo(a.area));

            Squarify(items, 0, bounds);
        }

        private void Squarify(List<(TreemapNode node, double area)> items, int startIndex, Rect bounds)
        {
            if (startIndex >= items.Count) return;
            if (bounds.Width < MinNodeSide || bounds.Height < MinNodeSide) return;

            double w = Math.Min(bounds.Width, bounds.Height); // shortest side
            var row = new List<(TreemapNode node, double area)>();

            int i = startIndex;
            while (i < items.Count)
            {
                var candidate = items[i];
                double newWorst = WorstRatio(row, candidate.area, w);

                if (row.Count == 0 || newWorst <= WorstRatio(row, 0, w))
                {
                    row.Add(candidate);
                    i++;
                }
                else
                {
                    break;
                }
            }

            Rect remaining = LayoutRow(row, bounds);
            Squarify(items, i, remaining);
        }

        private Rect LayoutRow(List<(TreemapNode node, double area)> row, Rect bounds)
        {
            if (row.Count == 0) return bounds;

            double rowArea = 0;
            foreach (var r in row) rowArea += r.area;

            bool horizontal = bounds.Width >= bounds.Height;
            double rowThickness = horizontal
                ? rowArea / bounds.Height
                : rowArea / bounds.Width;

            double pos = horizontal ? bounds.Y : bounds.X;
            double length = horizontal ? bounds.Height : bounds.Width;

            foreach (var (node, area) in row)
            {
                double nodeLength = (area / rowArea) * length;

                Rect nodeRect = horizontal
                    ? new Rect(bounds.X, pos, rowThickness, nodeLength)
                    : new Rect(pos, bounds.Y, nodeLength, rowThickness);
                pos += nodeLength;

                // Register — parent always added before children (correct render order).
                LayoutResult[node] = nodeRect;
                RenderOrder.Add((node, nodeRect));

                if (!node.IsLeaf && nodeRect.Width >= MinNodeSide && nodeRect.Height >= MinNodeSide)
                {
                    // Reserve a header band at the top when the node is tall enough.
                    bool hasHeader = nodeRect.Height >= FolderHeaderHeight + Inset + MinNodeSide;
                    double topInset = hasHeader ? FolderHeaderHeight : Inset;

                    Rect childBounds = new Rect(
                        nodeRect.X + Inset,
                        nodeRect.Y + topInset,
                        Math.Max(0, nodeRect.Width - Inset * 2),
                        Math.Max(0, nodeRect.Height - topInset - Inset));

                    if (childBounds.Width >= MinNodeSide && childBounds.Height >= MinNodeSide)
                        LayoutChildren(node.Children, node.Size, childBounds);
                }
            }

            if (horizontal)
            {
                return new Rect(
                    bounds.X + rowThickness, bounds.Y,
                    Math.Max(0, bounds.Width - rowThickness), bounds.Height);
            }
            else
            {
                return new Rect(
                    bounds.X, bounds.Y + rowThickness,
                    bounds.Width, Math.Max(0, bounds.Height - rowThickness));
            }
        }

        private static double WorstRatio(
            List<(TreemapNode node, double area)> row,
            double extra,
            double w)
        {
            double sum = extra;
            foreach (var r in row) sum += r.area;
            if (sum == 0 || w == 0) return double.MaxValue;

            double worst = 0;
            double ww = w * w;
            double ss = sum * sum;
            foreach (var (_, area) in row)
            {
                double r = Math.Max(ww * area / ss, ss / (ww * area));
                if (r > worst) worst = r;
            }
            if (extra > 0)
            {
                double r = Math.Max(ww * extra / ss, ss / (ww * extra));
                if (r > worst) worst = r;
            }
            return worst;
        }
    }
}
