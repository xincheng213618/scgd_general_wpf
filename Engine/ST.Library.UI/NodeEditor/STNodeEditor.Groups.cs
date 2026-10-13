using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ST.Library.UI.NodeEditor;

public enum STNodeGroupState { Idle, Running, Completed, Stopped, Failed }

/// <summary>Optional presentation group; membership and persistence are owned by the node.</summary>
public interface ISTNodeGroupMember
{
    string GroupId { get; set; }
    int GroupMemberCount { get; }
    string GroupTitle { get; }
    STNodeGroupState GroupState => STNodeGroupState.Idle;
}

public sealed class STNodeGroupEventArgs : EventArgs
{
    public STNodeGroup Group { get; }
    public STNodeGroupEventArgs(STNodeGroup group) => Group = group;
}

public sealed class STNodeGroup
{
    public STNode[] Nodes { get; }
    public Rectangle Bounds { get; }
    public Rectangle HeaderBounds { get; }
    public string Title { get; }
    public STNodeGroupState State { get; }

    internal STNodeGroup(STNode[] nodes)
    {
        Nodes = nodes;
        Rectangle bounds = nodes[0].Rectangle;
        foreach (STNode node in nodes)
        {
            bounds = Rectangle.Union(bounds, node.Rectangle);
            if (!string.IsNullOrEmpty(node.Mark)) bounds = Rectangle.Union(bounds, node.MarkRectangle);
        }
        Bounds = Rectangle.FromLTRB(bounds.Left - 18, bounds.Top - 34, bounds.Right + 18, bounds.Bottom + 18);
        HeaderBounds = new Rectangle(Bounds.Left, Bounds.Top, Bounds.Width, 30);
        Title = ((ISTNodeGroupMember)nodes[0]).GroupTitle;
        State = ((ISTNodeGroupMember)nodes[0]).GroupState;
    }
}

public partial class STNodeEditor
{
    public event EventHandler<STNodeGroupEventArgs> NodeGroupDoubleClick;

    public STNodeGroup[] GetNodeGroups() => Nodes.Cast<STNode>()
        .Where(node => node is ISTNodeGroupMember member && !string.IsNullOrEmpty(member.GroupId))
        .GroupBy(node => ((ISTNodeGroupMember)node).GroupId, StringComparer.Ordinal)
        .Select(group => new STNodeGroup(group.ToArray())).ToArray();

    private void DrawNodeGroups(DrawingTools tools)
    {
        using var text = new SolidBrush(VisualTheme?.Text ?? ForeColor);
        foreach (STNodeGroup group in GetNodeGroups())
        {
            Color accent = group.State switch
            {
                STNodeGroupState.Completed => VisualTheme?.Success ?? Color.ForestGreen,
                STNodeGroupState.Stopped => VisualTheme?.Warning ?? Color.DarkGoldenrod,
                STNodeGroupState.Failed => VisualTheme?.Error ?? Color.Firebrick,
                _ => VisualTheme?.Accent ?? Color.SteelBlue
            };
            using var fill = new SolidBrush(Color.FromArgb(16, accent));
            using var header = new SolidBrush(Color.FromArgb(32, accent));
            using var pen = new Pen(accent, group.State == STNodeGroupState.Running ? 2.5f : 1.5f);
            tools.Graphics.FillRectangle(fill, group.Bounds);
            tools.Graphics.FillRectangle(header, group.HeaderBounds);
            tools.Graphics.DrawRectangle(pen, group.Bounds);
            tools.Graphics.DrawString(group.Title, Font, text, group.Bounds.Left + 9, group.Bounds.Top + 8);
        }
    }

    private bool TryBeginNodeGroupInteraction(STNodeMouseEventArgs e)
    {
        STNodeGroup group = FindNodeGroupHeader(m_pt_down_in_canvas);
        if (group == null) return false;
        m_node_down = null;
        m_gp_hover = null;
        m_is_process_mouse_event = false;
        foreach (STNode node in GetSelectedNode()) node.SetSelected(false, false);
        foreach (STNode node in group.Nodes) node.SetSelected(true, false);
        SetActiveNode(group.Nodes[0]);
        Invalidate();
        if (!EnableEdit) return true;
        if (e.Clicks == 2)
        {
            EndPointerEdit();
            ReleaseMouseCapture();
            NodeGroupDoubleClick?.Invoke(this, new STNodeGroupEventArgs(group));
        }
        else if (!group.Nodes.Any(node => node.LockLocation))
        {
            m_dic_pt_selected.Clear();
            foreach (STNode node in group.Nodes) m_dic_pt_selected.Add(node, node.Location);
            m_ca = CanvasAction.MoveNode;
            BeginPointerEdit("移动节点组");
            if (_ShowMagnet) BuildMagnetLocation();
        }
        return true;
    }

    private STNodeGroup FindNodeGroupHeader(PointF point)
    {
        NodeFindInfo hit = FindNodeFromPoint(point);
        if (hit.Node != null || hit.NodeOption != null || !string.IsNullOrEmpty(hit.Mark)) return null;
        return GetNodeGroups().LastOrDefault(group => group.HeaderBounds.Contains(Point.Round(point)));
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
