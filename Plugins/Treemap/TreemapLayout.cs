using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Treemap
{
    /// <summary>
    /// Calculates squarified treemap layout.
    /// Each node receives a <see cref="Rect"/> stored in <see cref="LayoutResult"/>.
    /// </summary>
    public class TreemapLayout
    {
        /// <summary>Minimum side length (pixels) below which a node is not rendered.</summary>
        private const double MinNodeSide = 2.0;

        public Dictionary<TreemapNode, Rect> LayoutResult { get; } =
            new Dictionary<TreemapNode, Rect>();

        public void Calculate(TreemapNode root, Rect bounds)
        {
            LayoutResult.Clear();
            if (root == null || root.Size <= 0) return;

            if (root.IsLeaf)
            {
                LayoutResult[root] = bounds;
            }
            else
            {
                LayoutChildren(root.Children, root.Size, bounds);
            }
        }

        private void LayoutChildren(List<TreemapNode> nodes, double parentSize, Rect bounds)
        {
            if (nodes.Count == 0 || parentSize <= 0) return;
            if (bounds.Width < MinNodeSide || bounds.Height < MinNodeSide) return;

            double totalArea = bounds.Width * bounds.Height;

            // Build a list of (node, normalised area) pairs, filtering out zero-size nodes.
            var items = new List<(TreemapNode node, double area)>();
            foreach (var n in nodes)
            {
                if (n.Size <= 0) continue;
                double area = (n.Size / parentSize) * totalArea;
                if (area < MinNodeSide * MinNodeSide) continue;
                items.Add((n, area));
            }

            if (items.Count == 0) return;

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

        /// <summary>
        /// Lays out a completed row and returns the remaining rectangle.
        /// </summary>
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

                Rect nodeRect;
                if (horizontal)
                {
                    nodeRect = new Rect(bounds.X, pos, rowThickness, nodeLength);
                    pos += nodeLength;
                }
                else
                {
                    nodeRect = new Rect(pos, bounds.Y, nodeLength, rowThickness);
                    pos += nodeLength;
                }

                // Store this node's rect
                LayoutResult[node] = nodeRect;

                // Recurse into children
                if (!node.IsLeaf && nodeRect.Width >= MinNodeSide && nodeRect.Height >= MinNodeSide)
                {
                    // Add a small inset for visual nesting
                    const double inset = 2.0;
                    Rect childBounds = new Rect(
                        nodeRect.X + inset,
                        nodeRect.Y + inset,
                        Math.Max(0, nodeRect.Width - inset * 2),
                        Math.Max(0, nodeRect.Height - inset * 2));

                    LayoutChildren(node.Children, node.Size, childBounds);
                }
            }

            // Return remaining bounds
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

        /// <summary>
        /// Computes the worst aspect ratio for a row of items plus an optional extra item.
        /// </summary>
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
