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
    /// </summary>
    public class SugiyamaLayout
    {
        private readonly ConnectionInfo[] _connections;
        private readonly int _horizontalSpacing;
        private readonly int _verticalSpacing;
        private readonly int _startX;
        private readonly int _startY;

        // Graph adjacency (only reachable nodes)
        private Dictionary<STNode, List<STNode>> _children;
        private Dictionary<STNode, List<STNode>> _parents;

        // Layer assignment
        private Dictionary<STNode, int> _layerMap;
        private List<List<STNode>> _layers;

        // All reachable nodes
        private HashSet<STNode> _reachable;

        /// <summary>
        /// Number of forward+backward sweeps for crossing reduction.
        /// More iterations give better results at the cost of time.
        /// </summary>
        private const int CrossingReductionIterations = 24;

        public SugiyamaLayout(ConnectionInfo[] connections,
            int startX, int startY, int horizontalSpacing, int verticalSpacing)
        {
            _connections = connections;
            _horizontalSpacing = horizontalSpacing;
            _verticalSpacing = verticalSpacing;
            _startX = startX;
            _startY = startY;
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
        }

        /// <summary>
        /// Order nodes in <paramref name="layer"/> by the barycenter of their
        /// neighbors in the <paramref name="fixedLayer"/>.
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

            // Compute barycenter for each node in the layer
            var barycenters = new Dictionary<STNode, double>();
            for (int i = 0; i < layer.Count; i++)
            {
                var node = layer[i];
                var neighbors = useParents ? _parents[node] : _children[node];
                var relevantNeighbors = neighbors.Where(n => fixedPositions.ContainsKey(n)).ToList();

                if (relevantNeighbors.Count > 0)
                {
                    barycenters[node] = relevantNeighbors.Average(n => fixedPositions[n]);
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
