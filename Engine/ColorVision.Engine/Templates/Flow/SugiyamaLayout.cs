#pragma warning disable CA1854
using FlowEngineLib.Base;
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
    /// 3. Coordinate Assignment – Position nodes in readable lanes using neighbor centers.
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
        private Dictionary<STNode, List<STNode>> _layoutChildren;
        private Dictionary<STNode, List<STNode>> _layoutParents;

        // Layer assignment
        private Dictionary<STNode, int> _layerMap;
        private List<List<STNode>> _layers;

        // All reachable nodes
        private HashSet<STNode> _reachable;
        private STNode _rootNode;
        private int _layoutSlotHeight;

        // Port-level edge information for port-aware crossing reduction
        private struct PortEdge
        {
            public STNode From;
            public STNode To;
            public int OutputPortIndex;
            public int InputPortIndex;
            public int OutputPortCount;
            public int InputPortCount;
            public bool IsPrimaryLayoutEdge;
        }
        private List<PortEdge> _portEdges;

        private sealed class SharedFanOutPlacement
        {
            public STNode Node { get; init; }
            public int TargetColumn { get; init; }
            public List<STNode> AnchorTargets { get; init; } = new();
        }

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
            _rootNode = rootNode;
            BuildGraph(rootNode);
            if (_reachable.Count == 0) return;
            NormalizeReachableNodeDisplay();
            _layoutSlotHeight = Math.Max(90, _reachable.Max(node => node.Height));

            AssignLayers(rootNode);
            ReduceCrossings();
            AssignCoordinates();
            FoldIfNeeded();
            ReorderCommutativeMergeInputsForLayout();
        }

        /// <summary>
        /// Build adjacency lists from connection info, only for nodes reachable from root.
        /// </summary>
        private void BuildGraph(STNode rootNode)
        {
            _children = new Dictionary<STNode, List<STNode>>();
            _parents = new Dictionary<STNode, List<STNode>>();
            _layoutChildren = new Dictionary<STNode, List<STNode>>();
            _layoutParents = new Dictionary<STNode, List<STNode>>();
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
                _layoutChildren[node] = new List<STNode>();
                _layoutParents[node] = new List<STNode>();
            }

            // Build adjacency for reachable nodes only
            bool hasPrimaryLayoutEdge = false;
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

                    if (IsPrimaryLayoutEdge(conn))
                    {
                        hasPrimaryLayoutEdge = true;
                        if (!_layoutChildren[from].Contains(to))
                            _layoutChildren[from].Add(to);
                        if (!_layoutParents[to].Contains(from))
                            _layoutParents[to].Add(from);
                    }
                }
            }

            if (!hasPrimaryLayoutEdge)
            {
                foreach (var node in _reachable)
                {
                    _layoutChildren[node].AddRange(_children[node]);
                    _layoutParents[node].AddRange(_parents[node]);
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
                    InputPortCount = Math.Max(1, inCount),
                    IsPrimaryLayoutEdge = IsPrimaryLayoutEdge(conn)
                });
            }
        }

        private static bool IsPrimaryLayoutEdge(ConnectionInfo conn)
        {
            if (!IsFlowControlType(conn.Output.DataType) && !IsFlowControlType(conn.Input.DataType))
                return false;

            return IsControlOutputPort(conn.Output) && IsControlInputPort(conn.Input);
        }

        private static bool IsControlOutputPort(STNodeOption option)
        {
            string text = NormalizePortText(option.Text);
            return text == "OUT"
                || text == "--"
                || text == "OUT_START"
                || text.StartsWith("OUT_LOOP", StringComparison.Ordinal)
                || text.StartsWith("OUT_LP", StringComparison.Ordinal);
        }

        private static bool IsControlInputPort(STNodeOption option)
        {
            string text = NormalizePortText(option.Text);
            return text == "IN"
                || text == "--"
                || text == "IN_IMG"
                || text == "NODEIN"
                || text == "IN_START"
                || text.StartsWith("IN_LOOP", StringComparison.Ordinal)
                || text.StartsWith("IN_LP", StringComparison.Ordinal);
        }

        private static string NormalizePortText(string? text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : text.Trim().ToUpperInvariant();
        }

        private static bool IsFlowControlType(Type? type)
        {
            if (type == null) return false;
            return typeof(CVStartCFC).IsAssignableFrom(type) || typeof(CVLoopCFC).IsAssignableFrom(type);
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

                foreach (var child in _layoutChildren[node])
                {
                    int newLayer = currentLayer + 1;
                    if (!_layerMap.ContainsKey(child) || _layerMap[child] < newLayer)
                    {
                        _layerMap[child] = newLayer;
                        queue.Enqueue(child);
                    }
                }
            }

            // Place data-only satellites near the nearest already-layered parent without letting
            // data edges stretch the main execution backbone.
            bool changed;
            do
            {
                changed = false;
                foreach (var node in _reachable)
                {
                    if (_layerMap.ContainsKey(node))
                        continue;

                    var assignedParents = _parents[node].Where(parent => _layerMap.ContainsKey(parent)).ToList();
                    if (assignedParents.Count == 0)
                        continue;

                    _layerMap[node] = assignedParents.Max(parent => _layerMap[parent]) + 1;
                    changed = true;
                }
            }
            while (changed);

            // Assign any remaining nodes to layer 0
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

                var primaryEdges = relevantEdges.Where(edge => edge.IsPrimaryLayoutEdge).ToList();
                if (primaryEdges.Count > 0)
                    relevantEdges = primaryEdges;

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

            // Stable sort by barycenter value while keeping the previous order for ties.
            var originalOrder = layer.Select((node, index) => new { node, index })
                .ToDictionary(item => item.node, item => item.index);
            layer.Sort((a, b) =>
            {
                int compare = barycenters[a].CompareTo(barycenters[b]);
                return compare != 0 ? compare : originalOrder[a].CompareTo(originalOrder[b]);
            });
        }

        /// <summary>
        /// Phase 3: Assign (Left, Top) coordinates to each node.
        /// - X (Left) is determined by layer index × horizontal spacing.
        /// - Y (Top) is packed by neighbor centers so parallel branches stay readable.
        /// </summary>
        private void AssignCoordinates()
        {
            InitialStackLayers();

            // Alternate parent/child target passes. Each layer is packed around the
            // average center of its connected neighbors, which keeps parallel branches
            // as readable lanes instead of collapsing everything onto one horizontal line.
            for (int pass = 0; pass < 6; pass++)
            {
                for (int li = 1; li < _layers.Count; li++)
                {
                    PlaceLayerByNeighborTargets(li, useParents: true);
                }

                for (int li = _layers.Count - 2; li >= 0; li--)
                {
                    PlaceLayerByNeighborTargets(li, useParents: false);
                }
            }

            NormalizeLayoutOrigin();
        }

        private void InitialStackLayers()
        {
            for (int li = 0; li < _layers.Count; li++)
            {
                var layer = _layers[li];
                int x = _startX + li * _horizontalSpacing;
                int y = _startY;

                foreach (var node in layer)
                {
                    node.Left = x;
                    node.Top = GetSlotTop(node, y);
                    y += GetLayoutHeight(node) + _verticalSpacing;
                }
            }
        }

        private void PlaceLayerByNeighborTargets(int layerIndex, bool useParents)
        {
            var layer = _layers[layerIndex];
            if (layer.Count == 0) return;

            var originalOrder = layer.Select((node, index) => new { node, index })
                .ToDictionary(item => item.node, item => item.index);
            var targetCenters = new Dictionary<STNode, double>();

            foreach (var node in layer)
            {
                var neighbors = GetNeighborNodes(node, useParents);
                targetCenters[node] = neighbors.Count > 0
                    ? neighbors.Average(GetCenterY)
                    : GetCenterY(node);
            }

            layer.Sort((a, b) =>
            {
                int compare = targetCenters[a].CompareTo(targetCenters[b]);
                return compare != 0 ? compare : originalOrder[a].CompareTo(originalOrder[b]);
            });

            int x = _startX + layerIndex * _horizontalSpacing;
            double totalHeight = layer.Sum(GetLayoutHeight) + Math.Max(0, layer.Count - 1) * _verticalSpacing;
            double layerCenter = targetCenters.Values.Average();
            int y = (int)Math.Round(layerCenter - totalHeight / 2.0);

            foreach (var node in layer)
            {
                node.Left = x;
                node.Top = GetSlotTop(node, y);
                y += GetLayoutHeight(node) + _verticalSpacing;
            }
        }

        private List<STNode> GetNeighborNodes(STNode node, bool useParents)
        {
            var primaryNeighbors = useParents ? _layoutParents[node] : _layoutChildren[node];
            if (primaryNeighbors.Count > 0)
                return primaryNeighbors.Where(neighbor => _layerMap.ContainsKey(neighbor)).ToList();

            var allNeighbors = useParents ? _parents[node] : _children[node];
            return allNeighbors.Where(neighbor => _layerMap.ContainsKey(neighbor)).ToList();
        }

        private void NormalizeLayoutOrigin()
        {
            if (_reachable.Count == 0) return;

            int minX = _reachable.Min(node => node.Left);
            int minY = _reachable.Min(node => node.Top);
            int deltaX = _startX - minX;
            int deltaY = _startY - minY;

            if (deltaX == 0 && deltaY == 0) return;

            foreach (var node in _reachable)
            {
                node.Left += deltaX;
                node.Top += deltaY;
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
            if (TryApplyLaneFold())
                return;

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

        private bool TryApplyLaneFold()
        {
            if (_layers.Count < 12 || _reachable.Count < 12)
                return false;

            var (canvasW, canvasH) = GetBoundingBox();
            if (canvasW <= 0 || canvasH <= 0)
                return false;

            double aspectRatio = canvasW / Math.Max(1.0, canvasH);
            double layerDensity = _reachable.Count / Math.Max(1.0, _layers.Count);
            bool exceedsViewport = _viewportWidth > 0 && canvasW > _viewportWidth * 1.35;
            bool isLongThinFlow = aspectRatio > 2.4 || (exceedsViewport && layerDensity <= 2.2);
            if (!isLongThinFlow)
                return false;

            STNode? mergeNode = FindMajorMergeNode();
            if (mergeNode == null || !_layerMap.TryGetValue(mergeNode, out int mergeLayer) || mergeLayer < 6)
                return false;

            var serialLaneRows = BuildSerialLaneRows(_rootNode, mergeNode);
            if (serialLaneRows.Count >= 2)
            {
                ApplySerialLaneFold(serialLaneRows, mergeNode);
                NormalizeLayoutOrigin();
                return true;
            }

            var laneSegments = BuildLaneSegments(mergeLayer);
            if (laneSegments.Count < 2)
                return false;

            ApplyLaneFold(laneSegments, mergeLayer, mergeNode);
            NormalizeLayoutOrigin();
            return true;
        }

        private List<List<STNode>> BuildSerialLaneRows(STNode rootNode, STNode mergeNode)
        {
            var rows = new List<List<STNode>>();
            var currentRow = new List<STNode>();
            var visited = new HashSet<STNode>();
            STNode? current = rootNode;

            while (current != null && current != mergeNode && visited.Add(current))
            {
                currentRow.Add(current);

                var candidates = GetPrimaryChildrenToward(current, mergeNode)
                    .Where(child => child != mergeNode)
                    .ToList();
                if (candidates.Count == 0)
                    break;

                STNode? laneStartChild = candidates.FirstOrDefault(IsLaneStartNode);
                if (laneStartChild != null)
                {
                    rows.Add(currentRow);
                    currentRow = new List<STNode>();
                    current = laneStartChild;
                    continue;
                }

                current = ChooseSerialContinuationChild(current, candidates, mergeNode);
            }

            if (currentRow.Count > 0)
                rows.Add(currentRow);

            return rows
                .Where(row => row.Count > 0)
                .ToList();
        }

        private STNode? ChooseSerialContinuationChild(STNode current, List<STNode> candidates, STNode mergeNode)
        {
            var continuationCandidates = candidates
                .Where(child => child == mergeNode || !IsMultiInputMergeLikeNode(child))
                .ToList();
            var pool = continuationCandidates.Count > 0 ? continuationCandidates : candidates;

            return pool
                .OrderBy(child => GetContinuationInputPriority(current, child))
                .ThenByDescending(child => PrimaryDistanceToTarget(child, mergeNode, new HashSet<STNode>()))
                .ThenBy(child => GetOriginalPortOrder(current, child))
                .FirstOrDefault();
        }

        private bool IsMultiInputMergeLikeNode(STNode node)
        {
            if (node.InputOptionsCount >= 4)
                return true;

            return _layoutParents.TryGetValue(node, out var parents) && parents.Count >= 2;
        }

        private int GetContinuationInputPriority(STNode from, STNode to)
        {
            var inputTexts = _connections
                .Where(conn => conn.Output.Owner == from && conn.Input.Owner == to)
                .Select(conn => NormalizePortText(conn.Input.Text))
                .ToList();

            if (inputTexts.Any(text => text == "IN_IMG"))
                return 0;

            if (inputTexts.Any(text => text == "IN"))
                return 1;

            if (inputTexts.Any(text => text == "--" || text == "NODEIN"))
                return 2;

            if (inputTexts.Any(text => text.StartsWith("IMG", StringComparison.Ordinal)))
                return 3;

            return 4;
        }

        private List<STNode> GetPrimaryChildrenToward(STNode node, STNode target)
        {
            if (!_layoutChildren.TryGetValue(node, out var children))
                return new List<STNode>();

            return children
                .Where(child => PrimaryDistanceToTarget(child, target, new HashSet<STNode>()) >= 0)
                .OrderBy(child => GetOriginalPortOrder(node, child))
                .ToList();
        }

        private int PrimaryDistanceToTarget(STNode node, STNode target, HashSet<STNode> visiting)
        {
            if (node == target)
                return 0;

            if (!visiting.Add(node))
                return -1;

            if (!_layoutChildren.TryGetValue(node, out var children) || children.Count == 0)
                return -1;

            int best = -1;
            foreach (var child in children)
            {
                int distance = PrimaryDistanceToTarget(child, target, visiting);
                if (distance >= 0)
                    best = Math.Max(best, distance + 1);
            }

            visiting.Remove(node);
            return best;
        }

        private int GetOriginalPortOrder(STNode from, STNode to)
        {
            int order = int.MaxValue;
            foreach (var edge in _portEdges)
            {
                if (edge.From == from && edge.To == to)
                    order = Math.Min(order, edge.OutputPortIndex);
            }

            return order;
        }

        private void ApplySerialLaneFold(List<List<STNode>> rows, STNode mergeNode)
        {
            var layoutRows = rows
                .Select(row => row.ToList())
                .Where(row => row.Count > 0)
                .ToList();
            if (layoutRows.Count == 0)
                return;

            bool placeRootSeparately = layoutRows[0].Count > 0 && layoutRows[0][0] == _rootNode;
            if (placeRootSeparately)
            {
                layoutRows[0].RemoveAt(0);
                if (layoutRows[0].Count == 0)
                    layoutRows.RemoveAt(0);
            }

            int columnGap = GetHorizontalNodeGap();
            int rowGap = GetVerticalLaneGap();
            int rowY = _startY;
            int maxColumns = Math.Max(1, layoutRows.Max(row => row.Count));
            int rowHeight = layoutRows
                .SelectMany(row => row)
                .DefaultIfEmpty()
                .Max(node => node == null ? _layoutSlotHeight : GetLayoutHeight(node));
            int[] columnWidths = BuildColumnWidths(layoutRows, maxColumns);
            var mainRowNodes = new HashSet<STNode>(layoutRows.SelectMany(row => row));
            var sharedFanOuts = FindSharedFanOutPlacements(layoutRows, mainRowNodes, mergeNode);
            int[] extraBeforeColumns = BuildExtraBeforeColumns(maxColumns, sharedFanOuts, columnGap);
            int[] columnXs = BuildColumnXs(columnWidths, placeRootSeparately ? _rootNode.Width + columnGap : 0, columnGap, extraBeforeColumns);
            var rowCenters = new List<int>();
            var placed = new HashSet<STNode>();

            foreach (var row in layoutRows)
            {
                for (int column = 0; column < row.Count; column++)
                {
                    var node = row[column];
                    node.Left = columnXs[column];
                    node.Top = GetSlotTop(node, rowY + Math.Max(0, (rowHeight - GetLayoutHeight(node)) / 2));
                    placed.Add(node);
                }

                rowCenters.Add(rowY + rowHeight / 2);
                rowY += rowHeight + rowGap;
            }

            int foldedCenterY = rowCenters.Count > 0
                ? (rowCenters.Min() + rowCenters.Max()) / 2
                : _startY;
            int preMergeSatelliteColumns = EstimatePreMergeSatelliteColumns(mainRowNodes, mergeNode);
            int preMergeSatelliteWidth = EstimatePreMergeSatelliteWidth(mainRowNodes, mergeNode);
            int mergeX = columnXs[maxColumns - 1]
                + columnWidths[maxColumns - 1]
                + columnGap
                + preMergeSatelliteColumns * (preMergeSatelliteWidth + columnGap);

            if (placeRootSeparately)
            {
                _rootNode.Left = _startX;
                _rootNode.Top = GetTopForCenter(_rootNode, rowCenters[0]);
                placed.Add(_rootNode);
            }

            mergeNode.Left = mergeX;
            mergeNode.Top = GetTopForCenter(mergeNode, foldedCenterY);
            placed.Add(mergeNode);

            PlacePrimaryDownstreamChain(mergeNode, foldedCenterY, placed);
            PlaceSharedFanOuts(sharedFanOuts, layoutRows, columnXs, columnGap, placed);
            PlaceSerialSatellites(mergeNode, mergeX, placed);
            SeparateColocatedSharedTargets(sharedFanOuts, mergeNode, placed);
        }

        private static bool IsCommutativeMergeNode(STNode node)
        {
            string typeName = node.GetType().FullName ?? node.GetType().Name;
            if (typeName == "FlowEngineLib.Logical.LogicalANDNode")
                return true;

            string title = node.OnGetDrawTitle() ?? node.Title ?? string.Empty;
            return title.Contains("逻辑与", StringComparison.OrdinalIgnoreCase)
                || title.Contains("LogicalAND", StringComparison.OrdinalIgnoreCase);
        }

        private void ReorderCommutativeMergeInputsForLayout()
        {
            foreach (var mergeNode in _reachable.Where(IsCommutativeMergeNode))
            {
                var inputOptions = mergeNode.GetAllInputOptions()?.ToList();
                if (inputOptions == null || inputOptions.Count <= 2)
                    continue;

                var connectedInputs = _connections
                    .Where(conn => conn.Input.Owner == mergeNode
                        && conn.Input != STNodeOption.Empty
                        && IsFlowControlType(conn.Input.DataType))
                    .GroupBy(conn => conn.Input)
                    .Select(group => new
                    {
                        Input = group.Key,
                        SourceCenterY = group.Average(conn => GetCenterY(conn.Output.Owner)),
                        SourceLeft = group.Min(conn => conn.Output.Owner.Left)
                    })
                    .OrderBy(item => item.SourceCenterY)
                    .ThenBy(item => item.SourceLeft)
                    .Select(item => item.Input)
                    .ToList();

                if (connectedInputs.Count <= 1)
                    continue;

                var connectedSet = new HashSet<STNodeOption>(connectedInputs);
                var reordered = connectedInputs
                    .Concat(inputOptions.Where(option => !connectedSet.Contains(option)))
                    .ToList();

                mergeNode.ReorderInputOptions(reordered);
            }
        }

        private int GetHorizontalNodeGap()
        {
            return Math.Max(35, Math.Min(90, _horizontalSpacing / 2));
        }

        private int GetVerticalLaneGap()
        {
            return Math.Max(25, Math.Min(45, _verticalSpacing / 2));
        }

        private void NormalizeReachableNodeDisplay()
        {
            foreach (var node in _reachable)
            {
                if (node is CVCommonNode commonNode)
                {
                    commonNode.ApplyCompactNodeDisplay();
                }
                else
                {
                    node.SetAutoSize(true);
                }
            }
        }

        private int GetLayoutHeight(STNode node)
        {
            return Math.Max(node.Height, _layoutSlotHeight);
        }

        private int GetSlotTop(STNode node, int slotTop)
        {
            return slotTop + Math.Max(0, (GetLayoutHeight(node) - node.Height) / 2);
        }

        private static double GetCenterY(STNode node)
        {
            return node.Top + node.Height / 2.0;
        }

        private static int GetTopForCenter(STNode node, double centerY)
        {
            return (int)Math.Round(centerY - node.Height / 2.0);
        }

        private static int[] BuildColumnWidths(List<List<STNode>> rows, int maxColumns)
        {
            var widths = new int[maxColumns];
            for (int column = 0; column < maxColumns; column++)
            {
                widths[column] = rows
                    .Where(row => row.Count > column)
                    .Select(row => row[column].Width)
                    .DefaultIfEmpty(120)
                    .Max();
            }

            return widths;
        }

        private int[] BuildColumnXs(int[] columnWidths, int startOffset, int columnGap, int[]? extraBeforeColumns = null)
        {
            var xs = new int[columnWidths.Length];
            xs[0] = _startX + startOffset;
            for (int column = 1; column < columnWidths.Length; column++)
            {
                int extraBefore = extraBeforeColumns != null && column < extraBeforeColumns.Length
                    ? extraBeforeColumns[column]
                    : 0;
                xs[column] = xs[column - 1] + columnWidths[column - 1] + columnGap + extraBefore;
            }

            return xs;
        }

        private List<SharedFanOutPlacement> FindSharedFanOutPlacements(
            List<List<STNode>> rows,
            HashSet<STNode> mainRowNodes,
            STNode mergeNode)
        {
            var columnMap = BuildColumnMap(rows);
            var placements = new List<SharedFanOutPlacement>();

            foreach (var node in _reachable)
            {
                if (node == _rootNode || node == mergeNode)
                    continue;

                if (!_layoutParents.TryGetValue(node, out var primaryParents)
                    || !primaryParents.Any(mainRowNodes.Contains))
                {
                    continue;
                }

                if (!_children.TryGetValue(node, out var children))
                    continue;

                var allMainTargets = children
                    .Where(mainRowNodes.Contains)
                    .Where(columnMap.ContainsKey)
                    .ToList();
                if (allMainTargets.Count == 0)
                    continue;

                var targetColumns = allMainTargets
                    .GroupBy(child => columnMap[child])
                    .Where(group => group.Count() >= 2)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key)
                    .ToList();

                if (targetColumns.Count == 0)
                    continue;

                int targetColumn = allMainTargets.Min(child => columnMap[child]);
                if (targetColumn <= 0)
                    continue;

                placements.Add(new SharedFanOutPlacement
                {
                    Node = node,
                    TargetColumn = targetColumn,
                    AnchorTargets = targetColumns[0].ToList()
                });
            }

            return placements
                .OrderBy(item => item.TargetColumn)
                .ThenBy(item => _layerMap.TryGetValue(item.Node, out int layer) ? layer : int.MaxValue)
                .ToList();
        }

        private static Dictionary<STNode, int> BuildColumnMap(List<List<STNode>> rows)
        {
            var map = new Dictionary<STNode, int>();
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                for (int column = 0; column < row.Count; column++)
                    map[row[column]] = column;
            }

            return map;
        }

        private static int[] BuildExtraBeforeColumns(int maxColumns, List<SharedFanOutPlacement> sharedFanOuts, int columnGap)
        {
            var extra = new int[maxColumns];
            foreach (var placement in sharedFanOuts)
            {
                if (placement.TargetColumn <= 0 || placement.TargetColumn >= maxColumns)
                    continue;

                extra[placement.TargetColumn] = Math.Max(
                    extra[placement.TargetColumn],
                    placement.Node.Width + columnGap);
            }

            return extra;
        }

        private void PlaceSharedFanOuts(
            List<SharedFanOutPlacement> sharedFanOuts,
            List<List<STNode>> rows,
            int[] columnXs,
            int columnGap,
            HashSet<STNode> placed)
        {
            if (sharedFanOuts.Count == 0)
                return;

            foreach (var placement in sharedFanOuts)
            {
                if (placement.TargetColumn >= columnXs.Length)
                    continue;

                bool wasPlaced = placed.Remove(placement.Node);

                var targets = placement.AnchorTargets
                    .Where(placed.Contains)
                    .ToList();
                if (targets.Count == 0)
                {
                    if (wasPlaced)
                        placed.Add(placement.Node);
                    continue;
                }

                int x = columnXs[placement.TargetColumn] - placement.Node.Width - columnGap;
                int y = GetTopForCenter(placement.Node, AverageCenterY(targets));
                PlaceNodeAvoiding(placement.Node, x, y, placed);
            }
        }

        private int EstimatePreMergeSatelliteColumns(HashSet<STNode> mainRowNodes, STNode mergeNode)
        {
            var satellites = _reachable
                .Where(node => node != mergeNode && !mainRowNodes.Contains(node) && node != _rootNode)
                .ToList();

            bool hasDataMergeChain = satellites.Any(node =>
                _parents.TryGetValue(node, out var parents)
                && parents.Count(parent => mainRowNodes.Contains(parent) || satellites.Contains(parent)) >= 2);
            if (hasDataMergeChain)
                return 2;

            bool hasDirectMergeSatellite = _layoutParents.TryGetValue(mergeNode, out var mergeParents)
                && mergeParents.Any(parent => satellites.Contains(parent));
            return hasDirectMergeSatellite ? 1 : 0;
        }

        private int EstimatePreMergeSatelliteWidth(HashSet<STNode> mainRowNodes, STNode mergeNode)
        {
            return _reachable
                .Where(node => node != mergeNode && !mainRowNodes.Contains(node) && node != _rootNode)
                .Select(node => node.Width)
                .DefaultIfEmpty(120)
                .Max();
        }

        private void PlacePrimaryDownstreamChain(STNode startNode, int centerY, HashSet<STNode> placed)
        {
            STNode? current = startNode;
            int column = 1;
            var visited = new HashSet<STNode> { startNode };
            int columnGap = GetHorizontalNodeGap();
            int nextX = startNode.Left + startNode.Width + columnGap;

            while (current != null && _layoutChildren.TryGetValue(current, out var children))
            {
                var next = children
                    .Where(child => !visited.Contains(child))
                    .OrderBy(child => GetOriginalPortOrder(current, child))
                    .FirstOrDefault();

                if (next == null)
                    break;

                next.Left = nextX;
                next.Top = GetTopForCenter(next, centerY);
                placed.Add(next);
                visited.Add(next);
                nextX += next.Width + columnGap;
                current = next;
                column++;
            }
        }

        private void PlaceSerialSatellites(STNode mergeNode, int mergeX, HashSet<STNode> placed)
        {
            bool moved;
            int pass = 0;
            do
            {
                moved = false;
                pass++;

                foreach (var node in _reachable
                    .Where(node => !placed.Contains(node))
                    .OrderBy(node => _layerMap.TryGetValue(node, out int layer) ? layer : int.MaxValue))
                {
                    var placedParents = _parents[node].Where(placed.Contains).ToList();
                    var placedPrimaryParents = _layoutParents[node].Where(placed.Contains).ToList();
                    if (placedParents.Count == 0 && placedPrimaryParents.Count == 0)
                        continue;

                    bool isDirectMergeParent = _layoutParents.TryGetValue(mergeNode, out var mergeParents)
                        && mergeParents.Contains(node);
                    bool isDataMerge = placedParents.Count >= 2 && placedPrimaryParents.Count == 0;

                    int x;
                    int y;
                    if (IsInlineContinuationSatellite(node, mergeNode, placedPrimaryParents))
                    {
                        int columnGap = GetHorizontalNodeGap();
                        var parent = placedPrimaryParents[0];
                        x = parent.Left + parent.Width + columnGap;
                        y = GetTopForCenter(node, GetCenterY(parent));
                    }
                    else if (isDirectMergeParent)
                    {
                        var parents = placedPrimaryParents.Count > 0 ? placedPrimaryParents : placedParents;
                        int columnGap = GetHorizontalNodeGap();
                        x = Math.Min(mergeX - node.Width - columnGap, parents.Max(parent => parent.Left + parent.Width) + columnGap);
                        y = GetTopForCenter(node, AverageCenterY(parents));
                    }
                    else if (isDataMerge)
                    {
                        int columnGap = GetHorizontalNodeGap();
                        x = Math.Min(mergeX - node.Width - columnGap, placedParents.Max(parent => parent.Left + parent.Width) + columnGap);
                        y = GetTopForCenter(node, AverageCenterY(placedParents));
                    }
                    else
                    {
                        int columnGap = GetHorizontalNodeGap();
                        var parent = placedPrimaryParents.FirstOrDefault() ?? placedParents.First();
                        x = parent.Left;
                        y = HasMultipleDataChildren(node)
                            ? parent.Top - GetLayoutHeight(node) - GetVerticalLaneGap()
                            : parent.Top + GetLayoutHeight(parent) + Math.Max(25, _verticalSpacing / 2);
                    }

                    PlaceNodeAvoiding(node, x, y, placed);
                    moved = true;
                }
            }
            while (moved && pass < _reachable.Count);
        }

        private void SeparateColocatedSharedTargets(
            List<SharedFanOutPlacement> sharedFanOuts,
            STNode mergeNode,
            HashSet<STNode> placed)
        {
            if (sharedFanOuts.Count == 0)
                return;

            int columnGap = GetHorizontalNodeGap();
            int shiftX = sharedFanOuts
                .Select(placement => placement.Node.Width + columnGap)
                .DefaultIfEmpty(columnGap)
                .Max();

            foreach (var placement in sharedFanOuts)
            {
                if (!_children.TryGetValue(placement.Node, out var children))
                    continue;

                var anchorSet = new HashSet<STNode>(placement.AnchorTargets);
                var shifted = new HashSet<STNode>();
                foreach (var child in children.Where(placed.Contains).Where(child => !anchorSet.Contains(child)))
                {
                    int sourceCenterY = (int)Math.Round(GetCenterY(placement.Node));
                    int childCenterY = (int)Math.Round(GetCenterY(child));
                    bool sameColumn = Math.Abs(child.Left - placement.Node.Left) <= columnGap / 2;
                    bool farApart = Math.Abs(childCenterY - sourceCenterY) > placement.Node.Height + GetVerticalLaneGap();
                    if (!sameColumn || !farApart)
                        continue;

                    ShiftPrimaryChainRight(child, shiftX, mergeNode, shifted);
                }
            }
        }

        private void ShiftPrimaryChainRight(STNode node, int deltaX, STNode stopNode, HashSet<STNode> visited)
        {
            if (node == stopNode || !visited.Add(node))
                return;

            node.Left += deltaX;

            if (!_layoutChildren.TryGetValue(node, out var children))
                return;

            foreach (var child in children)
                ShiftPrimaryChainRight(child, deltaX, stopNode, visited);
        }

        private bool IsInlineContinuationSatellite(STNode node, STNode mergeNode, List<STNode> placedPrimaryParents)
        {
            if (placedPrimaryParents.Count != 1 || IsLaneStartNode(node) || IsMultiInputMergeLikeNode(node))
                return false;

            if (!_layoutChildren.TryGetValue(node, out var primaryChildren) || primaryChildren.Count == 0)
                return false;

            return primaryChildren.Any(child =>
                child == mergeNode || PrimaryDistanceToTarget(child, mergeNode, new HashSet<STNode>()) >= 0);
        }

        private bool HasMultipleDataChildren(STNode node)
        {
            return GetNonPrimaryChildren(node).Count >= 2;
        }

        private List<STNode> GetNonPrimaryChildren(STNode node)
        {
            if (!_children.TryGetValue(node, out var children) || children.Count == 0)
                return new List<STNode>();

            _layoutChildren.TryGetValue(node, out var primaryChildren);
            return children
                .Where(child => primaryChildren == null || !primaryChildren.Contains(child))
                .ToList();
        }

        private static int AverageCenterY(List<STNode> nodes)
        {
            return (int)Math.Round(nodes.Average(GetCenterY));
        }

        private void PlaceNodeAvoiding(STNode node, int x, int y, HashSet<STNode> placed)
        {
            node.Left = x;
            node.Top = y;

            int guard = 0;
            while (placed.Any(other => IntersectsWithMargin(node, other)) && guard < _reachable.Count)
            {
                node.Top += GetLayoutHeight(node) + Math.Max(25, _verticalSpacing / 2);
                guard++;
            }

            placed.Add(node);
        }

        private static bool IntersectsWithMargin(STNode a, STNode b)
        {
            const int marginX = 24;
            const int marginY = 18;
            return a.Left - marginX < b.Left + b.Width
                && a.Left + a.Width + marginX > b.Left
                && a.Top - marginY < b.Top + b.Height
                && a.Top + a.Height + marginY > b.Top;
        }

        private STNode? FindMajorMergeNode()
        {
            return _reachable
                .Where(node => _layerMap.ContainsKey(node))
                .Select(node => new
                {
                    Node = node,
                    Layer = _layerMap[node],
                    ParentCount = _parents[node].Count(parent => _reachable.Contains(parent)),
                    InputCount = node.InputOptionsCount
                })
                .Where(item => item.Layer > 0 && (item.ParentCount >= 4 || item.InputCount >= 5))
                .OrderByDescending(item => item.ParentCount)
                .ThenBy(item => item.Layer)
                .Select(item => item.Node)
                .FirstOrDefault();
        }

        private List<(int Start, int End)> BuildLaneSegments(int mergeLayer)
        {
            var segments = new List<(int Start, int End)>();
            int segmentStart = 0;
            int maxColumnsPerLane = Math.Max(4, Math.Min(6, _viewportWidth > 0 ? _viewportWidth / Math.Max(1, _horizontalSpacing) - 2 : 5));

            for (int layerIndex = 1; layerIndex < mergeLayer; layerIndex++)
            {
                int currentColumns = layerIndex - segmentStart;
                bool shouldBreak = currentColumns >= maxColumnsPerLane
                    || ShouldStartNewLane(layerIndex, segmentStart);

                if (!shouldBreak)
                    continue;

                if (layerIndex > segmentStart)
                    segments.Add((segmentStart, layerIndex));

                segmentStart = layerIndex;
            }

            if (mergeLayer > segmentStart)
                segments.Add((segmentStart, mergeLayer));

            return segments;
        }

        private bool ShouldStartNewLane(int layerIndex, int segmentStart)
        {
            if (layerIndex - segmentStart < 3)
                return false;

            return _layers[layerIndex].Any(IsLaneStartNode);
        }

        private bool IsLaneStartNode(STNode node)
        {
            if (!_layoutParents.TryGetValue(node, out var parents) || parents.Count == 0)
                return false;

            if (parents.Any(parent => _layerMap.TryGetValue(parent, out int parentLayer) && parentLayer <= 0))
                return false;

            string typeName = node.GetType().Name;
            string title = node.OnGetDrawTitle() ?? string.Empty;
            if (!typeName.Contains("Sensor", StringComparison.OrdinalIgnoreCase)
                && !title.Contains("传感器", StringComparison.Ordinal))
            {
                return false;
            }

            return !parents.Any(parent => parent.InputOptionsCount >= 5);
        }

        private void ApplyLaneFold(List<(int Start, int End)> laneSegments, int mergeLayer, STNode mergeNode)
        {
            int rowY = _startY;
            int rowGap = Math.Max(_verticalSpacing, 70);
            int maxColumns = laneSegments.Max(segment => segment.End - segment.Start);
            var rowCenters = new List<int>();

            foreach (var segment in laneSegments)
            {
                int rowHeight = GetSegmentHeight(segment.Start, segment.End);
                if (rowHeight <= 0)
                    rowHeight = _layers
                        .Skip(segment.Start)
                        .Take(segment.End - segment.Start)
                        .SelectMany(layer => layer)
                        .DefaultIfEmpty()
                        .Max(node => node == null ? _layoutSlotHeight : GetLayoutHeight(node));

                for (int layerIndex = segment.Start; layerIndex < segment.End; layerIndex++)
                {
                    int x = _startX + (layerIndex - segment.Start) * _horizontalSpacing;
                    PlaceLayerStack(layerIndex, x, rowY, rowHeight);
                }

                rowCenters.Add(rowY + rowHeight / 2);
                rowY += rowHeight + rowGap;
            }

            int foldedCenterY = rowCenters.Count > 0
                ? (rowCenters.Min() + rowCenters.Max()) / 2
                : _startY;
            int mergeX = _startX + (maxColumns + 1) * _horizontalSpacing;

            PlaceLayerAroundCenter(mergeLayer, mergeX, foldedCenterY);

            int downstreamLayerOffset = 1;
            for (int layerIndex = mergeLayer + 1; layerIndex < _layers.Count; layerIndex++)
            {
                int x = mergeX + downstreamLayerOffset * _horizontalSpacing;
                PlaceLayerAroundCenter(layerIndex, x, foldedCenterY);
                downstreamLayerOffset++;
            }

            PlaceDataMergeSatellites(mergeLayer, mergeNode, mergeX);

            // Keep the main merge node visually centered even if another node shares the layer.
            mergeNode.Top = GetTopForCenter(mergeNode, foldedCenterY);
        }

        private void PlaceDataMergeSatellites(int mergeLayer, STNode mergeNode, int mergeX)
        {
            var moved = new HashSet<STNode>();
            foreach (var node in _reachable)
            {
                if (!IsDataMergeSatellite(node, mergeLayer, mergeNode))
                    continue;

                var parents = _parents[node].Where(parent => _reachable.Contains(parent)).ToList();
                if (parents.Count == 0)
                    continue;

                int parentCenter = (int)Math.Round(parents.Average(GetCenterY));
                int maxParentRight = parents.Max(parent => parent.Left + parent.Width);
                node.Left = Math.Min(mergeX - _horizontalSpacing * 2, maxParentRight + _horizontalSpacing);
                node.Top = GetTopForCenter(node, parentCenter);
                moved.Add(node);

                PlaceSatellitePrimaryChain(node, mergeNode, moved);
            }
        }

        private bool IsDataMergeSatellite(STNode node, int mergeLayer, STNode mergeNode)
        {
            if (node == mergeNode || !_layerMap.TryGetValue(node, out int layer) || layer >= mergeLayer)
                return false;

            if (_layoutParents.TryGetValue(node, out var primaryParents) && primaryParents.Count > 0)
                return false;

            return _parents.TryGetValue(node, out var parents) && parents.Count >= 2;
        }

        private void PlaceSatellitePrimaryChain(STNode node, STNode stopNode, HashSet<STNode> moved)
        {
            if (!_layoutChildren.TryGetValue(node, out var children))
                return;

            int childX = node.Left + _horizontalSpacing;
            int centerY = (int)Math.Round(GetCenterY(node));
            foreach (var child in children)
            {
                if (child == stopNode || moved.Contains(child))
                    continue;

                if (!_layoutParents.TryGetValue(child, out var parents) || parents.Count != 1)
                    continue;

                child.Left = childX;
                child.Top = GetTopForCenter(child, centerY);
                moved.Add(child);
                PlaceSatellitePrimaryChain(child, stopNode, moved);
            }
        }

        private int GetSegmentHeight(int startLayer, int endLayer)
        {
            int height = 0;
            for (int layerIndex = startLayer; layerIndex < endLayer; layerIndex++)
            {
                height = Math.Max(height, GetLayerStackHeight(_layers[layerIndex]));
            }
            return height;
        }

        private int GetLayerStackHeight(List<STNode> layer)
        {
            if (layer.Count == 0)
                return 0;

            return layer.Sum(GetLayoutHeight) + Math.Max(0, layer.Count - 1) * _verticalSpacing;
        }

        private void PlaceLayerStack(int layerIndex, int x, int rowY, int rowHeight)
        {
            var layer = _layers[layerIndex];
            int stackHeight = GetLayerStackHeight(layer);
            int y = rowY + Math.Max(0, (rowHeight - stackHeight) / 2);

            foreach (var node in layer)
            {
                node.Left = x;
                node.Top = GetSlotTop(node, y);
                y += GetLayoutHeight(node) + _verticalSpacing;
            }
        }

        private void PlaceLayerAroundCenter(int layerIndex, int x, int centerY)
        {
            var layer = _layers[layerIndex];
            int stackHeight = GetLayerStackHeight(layer);
            int y = centerY - stackHeight / 2;

            foreach (var node in layer)
            {
                node.Left = x;
                node.Top = GetSlotTop(node, y);
                y += GetLayoutHeight(node) + _verticalSpacing;
            }
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

    }
}
