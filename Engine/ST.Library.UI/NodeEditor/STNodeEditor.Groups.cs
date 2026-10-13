using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ST.Library.UI.NodeEditor;

/// <summary>Optional presentation group; membership and persistence are owned by the node.</summary>
public interface ISTNodeGroupMember
{
    string GroupId { get; set; }
    int GroupMemberCount { get; }
    string GroupTitle { get; }
}

public sealed class STNodeGroup
{
    public STNode[] Nodes { get; }
    public Rectangle Bounds { get; }
    public string Title { get; }

    internal STNodeGroup(STNode[] nodes)
    {
        Nodes = nodes;
        Rectangle bounds = nodes[0].Rectangle;
        foreach (STNode node in nodes.Skip(1)) bounds = Rectangle.Union(bounds, node.Rectangle);
        Bounds = Rectangle.FromLTRB(bounds.Left - 18, bounds.Top - 34, bounds.Right + 18, bounds.Bottom + 18);
        Title = ((ISTNodeGroupMember)nodes[0]).GroupTitle;
    }
}

public partial class STNodeEditor
{
    public STNodeGroup[] GetNodeGroups() => Nodes.Cast<STNode>()
        .Where(node => node is ISTNodeGroupMember member && !string.IsNullOrEmpty(member.GroupId))
        .GroupBy(node => ((ISTNodeGroupMember)node).GroupId, StringComparer.Ordinal)
        .Select(group => new STNodeGroup(group.ToArray())).ToArray();

    private void DrawNodeGroups(DrawingTools tools)
    {
        Color accent = VisualTheme?.Accent ?? Color.SteelBlue;
        using var fill = new SolidBrush(Color.FromArgb(16, accent));
        using var text = new SolidBrush(VisualTheme?.Text ?? ForeColor);
        using var pen = new Pen(accent, 1.5f);
        foreach (STNodeGroup group in GetNodeGroups())
        {
            tools.Graphics.FillRectangle(fill, group.Bounds);
            tools.Graphics.DrawRectangle(pen, group.Bounds);
            tools.Graphics.DrawString(group.Title, Font, text, group.Bounds.Left + 9, group.Bounds.Top + 8);
        }
    }

    private static void RemapImportedGroups(IReadOnlyList<STNode> nodes)
    {
        foreach (var group in nodes.OfType<ISTNodeGroupMember>()
            .Where(node => !string.IsNullOrEmpty(node.GroupId)).GroupBy(node => node.GroupId).ToArray())
        {
            // A partial selection must not silently create a different executable group.
            string id = group.All(node => node.GroupMemberCount == group.Count()) ? Guid.NewGuid().ToString() : string.Empty;
            foreach (ISTNodeGroupMember node in group) node.GroupId = id;
        }
    }
}
