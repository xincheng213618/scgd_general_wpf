using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Templates.Flow.Versioning
{
    /// <summary>
    /// Runtime-neutral representation used for hashing and review. It is a
    /// sidecar model and is never written into the STN/CVFlow payload.
    /// </summary>
    public sealed class FlowSemanticDocument
    {
        public List<FlowSemanticNode> Nodes { get; set; } = new();

        public List<FlowSemanticEdge> Edges { get; set; } = new();

        public FlowLayoutDocument Layout { get; set; } = new();

        public FlowSemanticDocument DeepClone()
        {
            var clone = new FlowSemanticDocument
            {
                Layout = Layout.DeepClone(),
            };
            foreach (FlowSemanticNode node in Nodes)
                clone.Nodes.Add(node.DeepClone());
            foreach (FlowSemanticEdge edge in Edges)
                clone.Edges.Add(edge.DeepClone());
            return clone;
        }
    }

    public sealed class FlowSemanticNode
    {
        public string NodeId { get; set; } = string.Empty;

        public string TypeKey { get; set; } = string.Empty;

        public Dictionary<string, string?> Properties { get; set; } =
            new(StringComparer.Ordinal);

        public FlowSemanticNode DeepClone()
        {
            return new FlowSemanticNode
            {
                NodeId = NodeId,
                TypeKey = TypeKey,
                Properties = new Dictionary<string, string?>(
                    Properties,
                    StringComparer.Ordinal),
            };
        }
    }

    public sealed class FlowSemanticEdge
    {
        public string SourceNodeId { get; set; } = string.Empty;

        public string SourcePort { get; set; } = string.Empty;

        public string TargetNodeId { get; set; } = string.Empty;

        public string TargetPort { get; set; } = string.Empty;

        public FlowSemanticEdge DeepClone()
        {
            return new FlowSemanticEdge
            {
                SourceNodeId = SourceNodeId,
                SourcePort = SourcePort,
                TargetNodeId = TargetNodeId,
                TargetPort = TargetPort,
            };
        }
    }

    public sealed class FlowLayoutDocument
    {
        public double ViewportX { get; set; }

        public double ViewportY { get; set; }

        public double Scale { get; set; } = 1;

        public List<FlowNodeLayout> Nodes { get; set; } = new();

        public FlowLayoutDocument DeepClone()
        {
            var clone = new FlowLayoutDocument
            {
                ViewportX = ViewportX,
                ViewportY = ViewportY,
                Scale = Scale,
            };
            foreach (FlowNodeLayout node in Nodes)
                clone.Nodes.Add(node.DeepClone());
            return clone;
        }
    }

    public sealed class FlowNodeLayout
    {
        public string NodeId { get; set; } = string.Empty;

        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public FlowNodeLayout DeepClone()
        {
            return new FlowNodeLayout
            {
                NodeId = NodeId,
                X = X,
                Y = Y,
                Width = Width,
                Height = Height,
            };
        }
    }
}
