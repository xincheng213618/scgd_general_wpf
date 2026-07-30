using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Engine.Templates.Flow.Versioning
{
    public static class FlowSemanticHash
    {
        public static string ComputeBinaryHash(byte[] snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            return ToHash(snapshot);
        }

        public static string ComputeSemanticHash(FlowSemanticDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);
            FlowSemanticDocumentValidator.Validate(document);
            var canonical = new CanonicalTextBuilder();

            foreach (FlowSemanticNode node in document.Nodes
                .OrderBy(item => item.NodeId, StringComparer.Ordinal))
            {
                canonical.Add("node");
                canonical.Add(node.NodeId);
                canonical.Add(node.TypeKey);
                foreach (KeyValuePair<string, string?> property in
                    node.Properties.OrderBy(
                        item => item.Key,
                        StringComparer.Ordinal))
                {
                    canonical.Add(property.Key);
                    canonical.Add(property.Value);
                }
                canonical.EndGroup();
            }

            foreach (FlowSemanticEdge edge in document.Edges
                .OrderBy(GetEdgeKey, StringComparer.Ordinal))
            {
                canonical.Add("edge");
                canonical.Add(edge.SourceNodeId);
                canonical.Add(edge.SourcePort);
                canonical.Add(edge.TargetNodeId);
                canonical.Add(edge.TargetPort);
                canonical.EndGroup();
            }

            foreach (FlowSubflowReference subflow in document.Subflows
                .OrderBy(GetSubflowKey, StringComparer.Ordinal))
            {
                canonical.Add("subflow");
                canonical.Add(subflow.CallNodeId);
                canonical.Add(subflow.FlowKey);
                canonical.Add(subflow.Binding);
                canonical.Add(subflow.Revision?.ToString(
                    CultureInfo.InvariantCulture));
                canonical.Add(subflow.WaitForCompletion ? "1" : "0");
                canonical.Add(subflow.CancelWithParent ? "1" : "0");
                AddMap(canonical, subflow.InputMappings);
                AddMap(canonical, subflow.OutputMappings);
                canonical.EndGroup();
            }

            foreach (FlowErrorRoute route in document.ErrorRoutes
                .OrderBy(GetErrorRouteKey, StringComparer.Ordinal))
            {
                canonical.Add("error");
                canonical.Add(route.SourceNodeId);
                canonical.Add(route.ErrorCode);
                canonical.Add(route.TargetNodeId);
                canonical.Add(route.TargetPort);
                canonical.Add(route.IsInterrupting ? "1" : "0");
                canonical.EndGroup();
            }

            foreach (FlowRetryPolicyReference retryPolicy in
                document.RetryPolicies
                    .OrderBy(GetRetryPolicyKey, StringComparer.Ordinal))
            {
                canonical.Add("retry");
                canonical.Add(retryPolicy.NodeId);
                canonical.Add(retryPolicy.MaxAttempts.ToString(
                    CultureInfo.InvariantCulture));
                canonical.Add(retryPolicy.InitialDelayMs.ToString(
                    CultureInfo.InvariantCulture));
                canonical.Add(ToInvariant(retryPolicy.Backoff));
                canonical.Add(retryPolicy.MaxDelayMs.ToString(
                    CultureInfo.InvariantCulture));
                foreach (string kind in retryPolicy.RetryableKinds
                    .OrderBy(item => item, StringComparer.Ordinal))
                {
                    canonical.Add(kind);
                }
                canonical.EndGroup();
            }

            return ToHash(canonical.ToString());
        }

        public static string ComputeLayoutHash(FlowSemanticDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);
            FlowSemanticDocumentValidator.Validate(document);
            FlowLayoutDocument layout = document.Layout ??
                new FlowLayoutDocument();
            var canonical = new CanonicalTextBuilder();
            canonical.Add(ToInvariant(layout.ViewportX));
            canonical.Add(ToInvariant(layout.ViewportY));
            canonical.Add(ToInvariant(layout.Scale));
            canonical.EndGroup();
            foreach (FlowNodeLayout node in layout.Nodes
                .OrderBy(item => item.NodeId, StringComparer.Ordinal))
            {
                canonical.Add(node.NodeId);
                canonical.Add(ToInvariant(node.X));
                canonical.Add(ToInvariant(node.Y));
                canonical.Add(ToInvariant(node.Width));
                canonical.Add(ToInvariant(node.Height));
                canonical.EndGroup();
            }
            return ToHash(canonical.ToString());
        }

        internal static string GetEdgeKey(FlowSemanticEdge edge)
        {
            return string.Join(
                "\u001f",
                edge.SourceNodeId,
                edge.SourcePort,
                edge.TargetNodeId,
                edge.TargetPort);
        }

        internal static string GetSubflowKey(FlowSubflowReference subflow)
        {
            var canonical = new CanonicalTextBuilder();
            canonical.Add(subflow.CallNodeId);
            canonical.Add(subflow.FlowKey);
            canonical.Add(subflow.Binding);
            canonical.Add(subflow.Revision?.ToString(
                CultureInfo.InvariantCulture));
            canonical.Add(subflow.WaitForCompletion ? "1" : "0");
            canonical.Add(subflow.CancelWithParent ? "1" : "0");
            AddMap(canonical, subflow.InputMappings);
            AddMap(canonical, subflow.OutputMappings);
            return canonical.ToString();
        }

        internal static string GetErrorRouteKey(FlowErrorRoute route)
        {
            return string.Join(
                "\u001f",
                route.SourceNodeId,
                route.ErrorCode,
                route.TargetNodeId,
                route.TargetPort,
                route.IsInterrupting ? "1" : "0");
        }

        internal static string GetRetryPolicyKey(
            FlowRetryPolicyReference retryPolicy)
        {
            var canonical = new CanonicalTextBuilder();
            canonical.Add(retryPolicy.NodeId);
            canonical.Add(retryPolicy.MaxAttempts.ToString(
                CultureInfo.InvariantCulture));
            canonical.Add(retryPolicy.InitialDelayMs.ToString(
                CultureInfo.InvariantCulture));
            canonical.Add(ToInvariant(retryPolicy.Backoff));
            canonical.Add(retryPolicy.MaxDelayMs.ToString(
                CultureInfo.InvariantCulture));
            foreach (string kind in retryPolicy.RetryableKinds
                .OrderBy(item => item, StringComparer.Ordinal))
            {
                canonical.Add(kind);
            }
            return canonical.ToString();
        }

        internal static string GetLayoutKey(FlowNodeLayout node)
        {
            return string.Join(
                "\u001f",
                node.NodeId,
                ToInvariant(node.X),
                ToInvariant(node.Y),
                ToInvariant(node.Width),
                ToInvariant(node.Height));
        }

        private static void AddMap(
            CanonicalTextBuilder builder,
            IReadOnlyDictionary<string, string> values)
        {
            foreach (KeyValuePair<string, string> value in values
                .OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                builder.Add(value.Key);
                builder.Add(value.Value);
            }
            builder.EndGroup();
        }

        private static string ToInvariant(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string ToHash(string value)
        {
            return ToHash(Encoding.UTF8.GetBytes(value));
        }

        private static string ToHash(byte[] value)
        {
            return Convert.ToHexString(SHA256.HashData(value))
                .ToLowerInvariant();
        }

        private sealed class CanonicalTextBuilder
        {
            private readonly StringBuilder value = new();

            public void Add(string? item)
            {
                if (item == null)
                {
                    value.Append("-1:");
                    return;
                }

                value.Append(item.Length.ToString(CultureInfo.InvariantCulture));
                value.Append(':');
                value.Append(item);
            }

            public void EndGroup()
            {
                value.Append('|');
            }

            public override string ToString()
            {
                return value.ToString();
            }
        }
    }
}
