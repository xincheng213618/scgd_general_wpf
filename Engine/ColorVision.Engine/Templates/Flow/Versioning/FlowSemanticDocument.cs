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

        public List<FlowErrorRoute> ErrorRoutes { get; set; } = new();

        public List<FlowRetryPolicyReference> RetryPolicies { get; set; } =
            new();

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
            foreach (FlowErrorRoute route in ErrorRoutes)
                clone.ErrorRoutes.Add(route.DeepClone());
            foreach (FlowRetryPolicyReference retryPolicy in RetryPolicies)
                clone.RetryPolicies.Add(retryPolicy.DeepClone());
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

    public sealed class FlowErrorRoute
    {
        public string SourceNodeId { get; set; } = string.Empty;

        public string ErrorCode { get; set; } = string.Empty;

        public string TargetNodeId { get; set; } = string.Empty;

        public string TargetPort { get; set; } = "in:0";

        public bool IsInterrupting { get; set; } = true;

        public FlowErrorRoute DeepClone()
        {
            return new FlowErrorRoute
            {
                SourceNodeId = SourceNodeId,
                ErrorCode = ErrorCode,
                TargetNodeId = TargetNodeId,
                TargetPort = TargetPort,
                IsInterrupting = IsInterrupting,
            };
        }
    }

    public sealed class FlowRetryPolicyReference
    {
        public string NodeId { get; set; } = string.Empty;

        public int MaxAttempts { get; set; } = 1;

        public int InitialDelayMs { get; set; }

        public double Backoff { get; set; } = 1;

        public int MaxDelayMs { get; set; }

        public List<string> RetryableKinds { get; set; } = new();

        public FlowRetryPolicyReference DeepClone()
        {
            return new FlowRetryPolicyReference
            {
                NodeId = NodeId,
                MaxAttempts = MaxAttempts,
                InitialDelayMs = InitialDelayMs,
                Backoff = Backoff,
                MaxDelayMs = MaxDelayMs,
                RetryableKinds = new List<string>(RetryableKinds),
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
