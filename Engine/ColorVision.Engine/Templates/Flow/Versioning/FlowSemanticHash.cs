using System;
using System.Buffers;
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

            return canonical.ComputeHash();
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
            return canonical.ComputeHash();
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

        private static string ToInvariant(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string ToHash(ReadOnlySpan<byte> value)
        {
            Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
            SHA256.HashData(value, digest);
            return Convert.ToHexStringLower(digest);
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

            public string ComputeHash()
            {
                const int stackCharacterLimit = 512;
                const int bufferSize = 64 * 1024;
                if (value.Length <= stackCharacterLimit)
                {
                    Span<char> text = stackalloc char[value.Length];
                    value.CopyTo(0, text, text.Length);
                    Span<byte> utf8 = stackalloc byte[text.Length * 3];
                    int length = Encoding.UTF8.GetBytes(text, utf8);
                    return ToHash(utf8[..length]);
                }

                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                // A surrogate pair can span two StringBuilder chunks.
                Encoder encoder = Encoding.UTF8.GetEncoder();
                byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                try
                {
                    Span<byte> utf8 = buffer.AsSpan(0, bufferSize);
                    foreach (ReadOnlyMemory<char> chunk in value.GetChunks())
                    {
                        ReadOnlySpan<char> remaining = chunk.Span;
                        while (!remaining.IsEmpty)
                        {
                            encoder.Convert(remaining, utf8, flush: false,
                                out int consumed, out int written, out _);
                            hash.AppendData(utf8[..written]);
                            remaining = remaining[consumed..];
                        }
                    }

                    encoder.Convert(ReadOnlySpan<char>.Empty, utf8, flush: true,
                        out _, out int tailLength, out _);
                    if (tailLength > 0)
                        hash.AppendData(utf8[..tailLength]);

                    Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
                    hash.GetHashAndReset(digest);
                    return Convert.ToHexStringLower(digest);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
                }
            }
        }
    }
}
