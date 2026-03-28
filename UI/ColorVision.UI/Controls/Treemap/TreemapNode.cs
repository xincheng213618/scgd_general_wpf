using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;

namespace ColorVision.UI.Controls
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
        /// Absolute file system path for this node, set when built from a real
        /// directory scan.  Null for synthetic / mock nodes.
        /// </summary>
        public string? FullPath { get; set; }

        /// <summary>
        /// Optional user-visible label that overrides <see cref="Name"/> in tooltips.
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// Arbitrary application-defined data associated with this node.
        /// Not serialised by <see cref="SaveToJson"/>/<see cref="LoadFromJson"/>.
        /// </summary>
        public object? Tag { get; set; }

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

        // ─── Serialisation ────────────────────────────────────────────────────

        /// <summary>Serialises the entire tree to a JSON file.</summary>
        public void SaveToJson(string filePath)
        {
            var dto = ToDto(this);
            var json = JsonConvert.SerializeObject(dto, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        /// <summary>Deserialises a tree that was previously saved with <see cref="SaveToJson"/>.</summary>
        public static TreemapNode? LoadFromJson(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            var json = File.ReadAllText(filePath);
            var dto = JsonConvert.DeserializeObject<NodeDto>(json);
            return dto != null ? FromDto(dto) : null;
        }

        // ─── Private DTO helpers (avoids serialising the Color struct via WPF) ─

        private sealed class NodeDto
        {
            public string Name { get; set; } = string.Empty;
            public double Size { get; set; }
            public string? FullPath { get; set; }
            public string? Label { get; set; }
            public List<NodeDto>? Children { get; set; }
        }

        private static NodeDto ToDto(TreemapNode n)
        {
            var dto = new NodeDto
            {
                Name = n.Name,
                Size = n.Size,
                FullPath = n.FullPath,
                Label = n.Label,
            };
            if (n.Children.Count > 0)
            {
                dto.Children = new List<NodeDto>(n.Children.Count);
                foreach (var c in n.Children)
                    dto.Children.Add(ToDto(c));
            }
            return dto;
        }

        private static TreemapNode FromDto(NodeDto dto)
        {
            var node = new TreemapNode
            {
                Name = dto.Name,
                Size = dto.Size,
                FullPath = dto.FullPath,
                Label = dto.Label,
            };
            if (dto.Children != null)
                foreach (var c in dto.Children)
                    node.AddChild(FromDto(c));
            return node;
        }
    }
}
