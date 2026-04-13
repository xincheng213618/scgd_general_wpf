using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Templates.Flow
{
    /// <summary>
    /// Implements the Sugiyama layered graph drawing algorithm for automatic flow node layout.
    /// The algorithm produces a hierarchical left-to-right layout that minimizes edge crossings.
    /// 
    /// Phases:
    /// 1. Layer Assignment   – Assign each node to a layer using longest-path-from-root (BFS).
    /// 2. Crossing Reduction – Reorder nodes within each layer using the barycenter heuristic.
    /// 3. Coordinate Assignment – Position nodes, center parents relative to children, fix overlaps.
    /// 4. Fold – If AutoSize would shrink nodes below a readable threshold, fold into multiple rows.
    /// </summary>
    public class SugiyamaLayout
    {
        private readonly ConnectionInfo[] _connections;
        private readonly int _horizontalSpacing;
        private readonly int _verticalSpacing;
        private readonly int _startX;
        private readonly int _startY;
        private readonly int _viewportWidth;
        private readonly int _viewportHeight;

        // Graph adjacency (only reachable nodes)
        private Dictionary<STNode, List<STNode>> _children;
        private Dictionary<STNode, List<STNode>> _parents;

        // Layer assignment
        private Dictionary<STNode, int> _layerMap;
        private List<List<STNode>> _layers;

        // All reachable nodes
        private HashSet<STNode> _reachable;

        // Port-level edge information for port-aware crossing reduction
        private struct PortEdge
        {
            public STNode From;
            public STNode To;
            public int OutputPortIndex;
            public int InputPortIndex;
            public int OutputPortCount;
            public int InputPortCount;
        }
        private List<PortEdge> _portEdges;

        /// <summary>
        /// Number of forward+backward sweeps for crossing reduction.
        /// More iterations give better results at the cost of time.
        /// </summary>
        private const int CrossingReductionIterations = 24;

        public SugiyamaLayout(ConnectionInfo[] connections,
            int startX, int startY, int horizontalSpacing, int verticalSpacing,
            int viewportWidth = 0, int viewportHeight = 0)
        {
            _connections = connections;
            _horizontalSpacing = horizontalSpacing;
            _verticalSpacing = verticalSpacing;
            _startX = startX;
            _startY = startY;
            _viewportWidth = viewportWidth;
            _viewportHeight = viewportHeight;
        }

        /// <summary>
        /// Execute the full Sugiyama layout starting from the given root node.
        /// Only nodes reachable from <paramref name="rootNode"/> are repositioned.
        /// </summary>
        public void Execute(STNode rootNode)
        {
            BuildGraph(rootNode);
            if (_reachable.Count == 0) return;

            AssignLayers(rootNode);
            ReduceCrossings();
            AssignCoordinates();
            FoldIfNeeded();
        }

        /// <summary>
        /// Build adjacency lists from connection info, only for nodes reachable from root.
        /// </summary>
        private void BuildGraph(STNode rootNode)
        {
            _children = new Dictionary<STNode, List<STNode>>();
            _parents = new Dictionary<STNode, List<STNode>>();
            _reachable = new HashSet<STNode>();

            // First pass: discover all reachable nodes via BFS
            var queue = new Queue<STNode>();
            queue.Enqueue(rootNode);
            _reachable.Add(rootNode);

            // Build a temporary adjacency from all connections
            var tempChildren = new Dictionary<STNode, List<STNode>>();
            foreach (var conn in _connections)
            {
                var from = conn.Output.Owner;
                var to = conn.Input.Owner;
                if (!tempChildren.TryGetValue(from, out var list))
                {
                    list = new List<STNode>();
                    tempChildren[from] = list;
                }
                if (!list.Contains(to))
                    list.Add(to);
            }

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (tempChildren.TryGetValue(node, out var kids))
                {
                    foreach (var child in kids)
                    {
                        if (_reachable.Add(child))
                        {
                            queue.Enqueue(child);
                        }
                    }
                }
            }

            // Initialize adjacency for reachable nodes
            foreach (var node in _reachable)
            {
                _children[node] = new List<STNode>();
                _parents[node] = new List<STNode>();
            }

            // Build adjacency for reachable nodes only
            foreach (var conn in _connections)
            {
                var from = conn.Output.Owner;
                var to = conn.Input.Owner;
                if (_reachable.Contains(from) && _reachable.Contains(to))
                {
                    if (!_children[from].Contains(to))
                        _children[from].Add(to);
                    if (!_parents[to].Contains(from))
                        _parents[to].Add(from);
                }
            }

            // Build port-level edge information for port-aware crossing reduction
            _portEdges = new List<PortEdge>();
            foreach (var conn in _connections)
            {
                var from = conn.Output.Owner;
                var to = conn.Input.Owner;
                if (!_reachable.Contains(from) || !_reachable.Contains(to)) continue;

                int outIdx = 0, outCount = 1;
                var outputOptions = from.GetOutputOptions();
                if (outputOptions != null)
                {
                    outIdx = Array.IndexOf(outputOptions, conn.Output);
                    if (outIdx < 0) outIdx = 0;
                    outCount = outputOptions.Length;
                }

                int inIdx = 0, inCount = 1;
                var inputOptions = to.GetInputOptions();
                if (inputOptions != null)
                {
                    inIdx = Array.IndexOf(inputOptions, conn.Input);
                    if (inIdx < 0) inIdx = 0;
                    inCount = inputOptions.Length;
                }

                _portEdges.Add(new PortEdge
                {
                    From = from,
                    To = to,
                    OutputPortIndex = outIdx,
                    InputPortIndex = inIdx,
                    OutputPortCount = Math.Max(1, outCount),
                    InputPortCount = Math.Max(1, inCount)
                });
            }
        }

        /// <summary>
        /// Phase 1: Assign each node to a layer using longest-path-from-root.
        /// A node's layer = max(parent layers) + 1.
        /// This ensures all edges point forward (left to right).
        /// </summary>
        private void AssignLayers(STNode rootNode)
        {
            _layerMap = new Dictionary<STNode, int>();

            // BFS-based longest path assignment
            // Process nodes in topological order, propagating maximum layer depth
            var queue = new Queue<STNode>();
            queue.Enqueue(rootNode);
            _layerMap[rootNode] = 0;

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                int currentLayer = _layerMap[node];

                foreach (var child in _children[node])
                {
                    int newLayer = currentLayer + 1;
                    if (!_layerMap.ContainsKey(child) || _layerMap[child] < newLayer)
                    {
                        _layerMap[child] = newLayer;
                        queue.Enqueue(child);
                    }
                }
            }

            // Assign any unreachable-from-root but reachable nodes to layer 0
            foreach (var node in _reachable)
            {
                if (!_layerMap.ContainsKey(node))
                    _layerMap[node] = 0;
            }

            // Build layer lists
            int maxLayer = _layerMap.Count > 0 ? _layerMap.Values.Max() : 0;
            _layers = new List<List<STNode>>();
            for (int i = 0; i <= maxLayer; i++)
            {
                _layers.Add(new List<STNode>());
            }
            foreach (var kvp in _layerMap)
            {
                _layers[kvp.Value].Add(kvp.Key);
            }
        }

        /// <summary>
        /// Phase 2: Reduce edge crossings using the barycenter heuristic.
        /// Alternates forward (layer 0→N) and backward (layer N→0) sweeps.
        /// Then applies swap-based optimization to escape local minima.
        /// </summary>
        private void ReduceCrossings()
        {
            if (_layers.Count <= 1) return;

            for (int iter = 0; iter < CrossingReductionIterations; iter++)
            {
                // Forward sweep: order each layer based on parents in the previous layer
                for (int i = 1; i < _layers.Count; i++)
                {
                    OrderLayerByBarycenter(_layers[i], _layers[i - 1], useParents: true);
                }

                // Backward sweep: order each layer based on children in the next layer
                for (int i = _layers.Count - 2; i >= 0; i--)
                {
                    OrderLayerByBarycenter(_layers[i], _layers[i + 1], useParents: false);
                }
            }

            // Post-processing: swap adjacent nodes to further reduce crossings
            SwapOptimize();
        }

        /// <summary>
        /// Order nodes in <paramref name="layer"/> by the port-aware barycenter of their
        /// neighbors in the <paramref name="fixedLayer"/>.
        /// Port indices are used as fractional offsets so that nodes connecting to upper
        /// ports tend to be placed above nodes connecting to lower ports.
        /// </summary>
        private void OrderLayerByBarycenter(List<STNode> layer, List<STNode> fixedLayer, bool useParents)
        {
            if (layer.Count <= 1) return;

            // Build position map for fixed layer
            var fixedPositions = new Dictionary<STNode, int>();
            for (int i = 0; i < fixedLayer.Count; i++)
            {
                fixedPositions[fixedLayer[i]] = i;
            }

            // Compute port-aware barycenter for each node in the layer
            var barycenters = new Dictionary<STNode, double>();
            for (int i = 0; i < layer.Count; i++)
            {
                var node = layer[i];

                // Gather relevant port edges
                var relevantEdges = new List<PortEdge>();
                foreach (var edge in _portEdges)
                {
                    if (useParents)
                    {
                        if (edge.To == node && fixedPositions.ContainsKey(edge.From))
                            relevantEdges.Add(edge);
                    }
                    else
                    {
                        if (edge.From == node && fixedPositions.ContainsKey(edge.To))
                            relevantEdges.Add(edge);
                    }
                }

                if (relevantEdges.Count > 0)
                {
                    double sum = 0;
                    foreach (var edge in relevantEdges)
                    {
                        if (useParents)
                        {
                            // Parent position + fractional offset for its output port
                            double portOffset = PortFraction(edge.OutputPortIndex, edge.OutputPortCount);
                            sum += fixedPositions[edge.From] + portOffset;
                        }
                        else
                        {
                            // Child position + fractional offset for its input port
                            double portOffset = PortFraction(edge.InputPortIndex, edge.InputPortCount);
                            sum += fixedPositions[edge.To] + portOffset;
                        }
                    }
                    barycenters[node] = sum / relevantEdges.Count;
                }
                else
                {
                    // Keep relative position for disconnected nodes
                    barycenters[node] = i;
                }
            }

            // Stable sort by barycenter value
            layer.Sort((a, b) => barycenters[a].CompareTo(barycenters[b]));
        }

        /// <summary>
        /// Phase 3: Assign (Left, Top) coordinates to each node.
        /// - X (Left) is determined by layer index × horizontal spacing.
        /// - Y (Top) is determined by position within the layer, then refined by centering
        ///   parents relative to their children.
        /// </summary>
        private void AssignCoordinates()
        {
            // Initial placement: spread nodes evenly in each layer
            for (int li = 0; li < _layers.Count; li++)
            {
                var layer = _layers[li];
                int x = _startX + li * _horizontalSpacing;
                int y = _startY;

                foreach (var node in layer)
                {
                    node.Left = x;
                    node.Top = y;
                    y += node.Height + _verticalSpacing;
                }
            }

            // Refinement: center parents relative to children (right-to-left pass)
            for (int li = _layers.Count - 2; li >= 0; li--)
            {
                foreach (var node in _layers[li])
                {
                    var kids = _children[node].Where(c => _layerMap.ContainsKey(c)).ToList();
                    if (kids.Count > 0)
                    {
                        int minY = kids.Min(c => c.Top);
                        int maxY = kids.Max(c => c.Top + c.Height);
                        int center = (minY + maxY) / 2 - node.Height / 2;
                        node.Top = center;
                    }
                }
                FixOverlaps(_layers[li]);
            }

            // Additional refinement: center children relative to parents (left-to-right pass)
            for (int li = 1; li < _layers.Count; li++)
            {
                foreach (var node in _layers[li])
                {
                    var pars = _parents[node].Where(p => _layerMap.ContainsKey(p)).ToList();
                    if (pars.Count > 0)
                    {
                        int minY = pars.Min(p => p.Top);
                        int maxY = pars.Max(p => p.Top + p.Height);
                        int desiredCenter = (minY + maxY) / 2 - node.Height / 2;
                        node.Top = desiredCenter;
                    }
                }
                FixOverlaps(_layers[li]);
            }

            // Final pass: center parents again (right-to-left) for best balance
            for (int li = _layers.Count - 2; li >= 0; li--)
            {
                foreach (var node in _layers[li])
                {
                    var kids = _children[node].Where(c => _layerMap.ContainsKey(c)).ToList();
                    if (kids.Count > 0)
                    {
                        int minY = kids.Min(c => c.Top);
                        int maxY = kids.Max(c => c.Top + c.Height);
                        int center = (minY + maxY) / 2 - node.Height / 2;
                        node.Top = center;
                    }
                }
                FixOverlaps(_layers[li]);
            }
        }

        /// <summary>
        /// Phase 4: If AutoSize would scale nodes below a readable size, fold the layout
        /// into multiple rows. All rows run left-to-right. The number of rows is chosen
        /// so the resulting scale factor stays above <see cref="MinReadableScale"/>.
        /// When the viewport is large enough that the unfolded layout scales fine, no
        /// folding happens at all.
        /// </summary>
        private const float MinReadableScale = 0.35f;

        private void FoldIfNeeded()
        {
            if (_layers.Count <= 2) return;
            if (_viewportWidth <= 0 || _viewportHeight <= 0) return;

            // Measure the current (unfolded) bounding box
            var (canvasW, canvasH) = GetBoundingBox();
            if (canvasW <= 0 || canvasH <= 0) return;

            // Simulate what AutoSize would compute
            float scale = Math.Min((float)_viewportWidth / canvasW, (float)_viewportHeight / canvasH);
            if (scale > 1f) scale = 1f;

            // If unfolded layout fits at a readable scale, nothing to do
            if (scale >= MinReadableScale) return;

            // Try increasing row counts until we find one that gives a good scale,
            // or stop when we can't improve further.
            int bestRows = 1;
            float bestScale = scale;

            for (int numRows = 2; numRows <= _layers.Count; numRows++)
            {
                int layersPerRow = (int)Math.Ceiling((double)_layers.Count / numRows);
                if (layersPerRow < 2) break;

                // Estimate folded dimensions
                var (foldedW, foldedH) = EstimateFoldedSize(numRows, layersPerRow);
                float foldedScale = Math.Min((float)_viewportWidth / foldedW, (float)_viewportHeight / foldedH);
                if (foldedScale > 1f) foldedScale = 1f;

                if (foldedScale > bestScale)
                {
                    bestScale = foldedScale;
                    bestRows = numRows;
                }

                // Good enough — stop searching
                if (foldedScale >= MinReadableScale) break;
            }

            if (bestRows <= 1) return;

            // Apply the fold
            int finalLayersPerRow = (int)Math.Ceiling((double)_layers.Count / bestRows);
            ApplyFold(bestRows, finalLayersPerRow);
        }

        private (int width, int height) GetBoundingBox()
        {
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var node in _reachable)
            {
                minX = Math.Min(minX, node.Left);
                maxX = Math.Max(maxX, node.Left + node.Width);
                minY = Math.Min(minY, node.Top);
                maxY = Math.Max(maxY, node.Top + node.Height);
            }
            return (maxX - minX, maxY - minY);
        }

        private (float width, float height) EstimateFoldedSize(int numRows, int layersPerRow)
        {
            float rowWidth = layersPerRow * _horizontalSpacing;
            int rowGap = _verticalSpacing * 3;

            float totalHeight = 0;
            for (int r = 0; r < numRows; r++)
            {
                int start = r * layersPerRow;
                int end = Math.Min(start + layersPerRow, _layers.Count);

                int rMin = int.MaxValue, rMax = int.MinValue;
                for (int li = start; li < end; li++)
                {
                    foreach (var node in _layers[li])
                    {
                        rMin = Math.Min(rMin, node.Top);
                        rMax = Math.Max(rMax, node.Top + node.Height);
                    }
                }
                if (rMin > rMax) { rMin = 0; rMax = 100; }
                totalHeight += (rMax - rMin);
                if (r < numRows - 1) totalHeight += rowGap;
            }

            return (rowWidth, totalHeight);
        }

        private void ApplyFold(int numRows, int layersPerRow)
        {
            // Compute per-row vertical extent
            var rowMinY = new int[numRows];
            var rowMaxY = new int[numRows];

            for (int r = 0; r < numRows; r++)
            {
                int start = r * layersPerRow;
                int end = Math.Min(start + layersPerRow, _layers.Count);

                int rMin = int.MaxValue, rMax = int.MinValue;
                for (int li = start; li < end; li++)
                {
                    foreach (var node in _layers[li])
                    {
                        rMin = Math.Min(rMin, node.Top);
                        rMax = Math.Max(rMax, node.Top + node.Height);
                    }
                }
                if (rMin > rMax) { rMin = 0; rMax = 0; }
                rowMinY[r] = rMin;
                rowMaxY[r] = rMax;
            }

            // Reposition: all rows left-to-right, stacked vertically
            int accumulatedY = _startY;
            int rowGap = _verticalSpacing * 3;

            for (int r = 0; r < numRows; r++)
            {
                int start = r * layersPerRow;
                int end = Math.Min(start + layersPerRow, _layers.Count);

                for (int li = start; li < end; li++)
                {
                    int col = li - start;
                    int x = _startX + col * _horizontalSpacing;

                    foreach (var node in _layers[li])
                    {
                        node.Left = x;
                        node.Top = accumulatedY + (node.Top - rowMinY[r]);
                    }
                }

                accumulatedY += (rowMaxY[r] - rowMinY[r]) + rowGap;
            }
        }

        /// <summary>
        /// Maps a port index to a fractional offset for barycenter calculation.
        /// Port 0 → −0.3, last port → +0.3, middle → 0.
        /// This encourages nodes connected via upper ports to be placed above
        /// those connected via lower ports, reducing crossings at multi-port nodes.
        /// </summary>
        private static double PortFraction(int portIndex, int portCount)
        {
            if (portCount <= 1) return 0;
            return ((double)portIndex / (portCount - 1) - 0.5) * 0.6;
        }

        /// <summary>
        /// Count edge crossings between layer at <paramref name="layerIdx"/> and the next layer,
        /// considering port positions for accurate crossing detection on multi-port nodes.
        /// </summary>
        private int CountLayerCrossings(int layerIdx)
        {
            if (layerIdx < 0 || layerIdx >= _layers.Count - 1) return 0;

            var layer1 = _layers[layerIdx];
            var layer2 = _layers[layerIdx + 1];

            // Build position maps
            var pos1 = new Dictionary<STNode, int>();
            for (int i = 0; i < layer1.Count; i++) pos1[layer1[i]] = i;
            var pos2 = new Dictionary<STNode, int>();
            for (int i = 0; i < layer2.Count; i++) pos2[layer2[i]] = i;

            // Collect edges between these two layers with port-aware positions
            var edges = new List<(double src, double tgt)>();
            foreach (var e in _portEdges)
            {
                if (pos1.ContainsKey(e.From) && pos2.ContainsKey(e.To))
                {
                    double src = pos1[e.From] + PortFraction(e.OutputPortIndex, e.OutputPortCount);
                    double tgt = pos2[e.To] + PortFraction(e.InputPortIndex, e.InputPortCount);
                    edges.Add((src, tgt));
                }
            }

            // Count crossings — O(E²), acceptable for typical flow graph sizes
            int crossings = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                for (int j = i + 1; j < edges.Count; j++)
                {
                    if ((edges[i].src < edges[j].src && edges[i].tgt > edges[j].tgt) ||
                        (edges[i].src > edges[j].src && edges[i].tgt < edges[j].tgt))
                    {
                        crossings++;
                    }
                }
            }
            return crossings;
        }

        /// <summary>
        /// Post-processing: try swapping adjacent nodes within each layer to further
        /// reduce edge crossings. This catches cases the barycenter heuristic misses,
        /// especially around multi-input nodes like logical AND (逻辑与).
        /// </summary>
        private void SwapOptimize()
        {
            bool improved = true;
            int maxPasses = 10;
            int pass = 0;

            while (improved && pass < maxPasses)
            {
                improved = false;
                pass++;

                for (int li = 0; li < _layers.Count; li++)
                {
                    var layer = _layers[li];
                    for (int i = 0; i < layer.Count - 1; i++)
                    {
                        // Count current crossings involving this layer
                        int currentCrossings = 0;
                        if (li > 0) currentCrossings += CountLayerCrossings(li - 1);
                        if (li < _layers.Count - 1) currentCrossings += CountLayerCrossings(li);

                        // Try swapping adjacent nodes
                        (layer[i], layer[i + 1]) = (layer[i + 1], layer[i]);

                        int newCrossings = 0;
                        if (li > 0) newCrossings += CountLayerCrossings(li - 1);
                        if (li < _layers.Count - 1) newCrossings += CountLayerCrossings(li);

                        if (newCrossings < currentCrossings)
                        {
                            improved = true; // Keep the swap
                        }
                        else
                        {
                            // Revert the swap
                            (layer[i], layer[i + 1]) = (layer[i + 1], layer[i]);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Fix vertical overlaps within a layer by pushing nodes down when they overlap.
        /// Preserves the current ordering.
        /// </summary>
        private void FixOverlaps(List<STNode> layer)
        {
            if (layer.Count <= 1) return;

            // Sort in-place by current Top to respect ordering
            layer.Sort((a, b) => a.Top.CompareTo(b.Top));

            // Push overlapping nodes down
            for (int i = 1; i < layer.Count; i++)
            {
                int requiredTop = layer[i - 1].Top + layer[i - 1].Height + _verticalSpacing;
                if (layer[i].Top < requiredTop)
                {
                    layer[i].Top = requiredTop;
                }
            }
        }
    }
}
