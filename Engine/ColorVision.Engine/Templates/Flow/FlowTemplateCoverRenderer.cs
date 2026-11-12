using ColorVision.Engine.FlowProcessing.Compilation;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Engine.Templates.Flow;

/// <summary>Saved geometry and display-only node metadata, drawn with the editor's palette and connection routing.</summary>
internal static class FlowTemplateCoverRenderer
{
    private sealed record Node(NeutralNode Source, STNodeSavedState State, Rectangle Bounds, STNodePreviewAppearance Appearance, STNodePortLayout? Ports);

    internal static byte[] Render(byte[] data, FlowTemplateCoverStyle style)
    {
        var canvas = StnV1NeutralCodec.Decode(data, new StnV1CodecOptions(), allowMissingNodes: true);
        Node[] nodes = canvas.Nodes.Select(ReadNode).ToArray();
        var byId = nodes.ToDictionary(node => node.Source.NodeId);
        using var bitmap = new Bitmap(FlowTemplateCoverService.PixelWidth, FlowTemplateCoverService.PixelHeight);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(style.Theme.Canvas);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        if (style.ShowGrid) DrawGrid(graphics, bitmap.Size, style.Theme);
        if (nodes.Length > 0)
        {
            // Fit the saved canvas without arranging or changing any node positions.
            RectangleF bounds = nodes.Select(node => (RectangleF)node.Bounds).Aggregate(RectangleF.Union);
            foreach (var edge in canvas.Connections)
            {
                var from = PortBounds(byId[edge.Output.Node.NodeId], false, edge.Output.LocalIndex);
                var to = PortBounds(byId[edge.Input.Node.NodeId], true, edge.Input.LocalIndex);
                using var path = STNodeEditor.CreateBezierPath(from.Right, from.Top + from.Height / 2,
                    to.Left - 1, to.Top + to.Height / 2, style.Curvature);
                bounds = RectangleF.Union(bounds, path.GetBounds());
            }
            bounds.Inflate(12, 12);
            float scale = Math.Min(2, Math.Min((bitmap.Width - 32f) / bounds.Width, (bitmap.Height - 32f) / bounds.Height));
            graphics.TranslateTransform((bitmap.Width - bounds.Width * scale) / 2, (bitmap.Height - bounds.Height * scale) / 2);
            graphics.ScaleTransform(scale, scale);
            graphics.TranslateTransform(-bounds.Left, -bounds.Top);
            foreach (var edge in canvas.Connections)
            {
                var source = byId[edge.Output.Node.NodeId];
                var target = byId[edge.Input.Node.NodeId];
                var from = PortBounds(source, false, edge.Output.LocalIndex);
                var to = PortBounds(target, true, edge.Input.LocalIndex);
                using var pen = new Pen(PortColor(source, false, edge.Output.LocalIndex, style.Theme), 1.5f);
                using var path = STNodeEditor.CreateBezierPath(from.Right, from.Top + from.Height / 2,
                    to.Left - 1, to.Top + to.Height / 2, style.Curvature);
                graphics.DrawPath(pen, path);
            }
            foreach (var node in nodes) DrawNode(graphics, node, style, scale);
        }
        using var output = new MemoryStream();
        bitmap.Save(output, ImageFormat.Png);
        return output.ToArray();
    }

    private static Node ReadNode(NeutralNode node)
    {
        var state = STNodeSavedState.Parse(node.Payload);
        int Read(string key) => state.Properties.TryGetValue(key, out var bytes) && bytes.Length == sizeof(int)
            ? BitConverter.ToInt32(bytes) : throw new InvalidDataException($"Invalid saved node geometry: {key}");
        var bounds = new Rectangle(Read("Left"), Read("Top"), Read("Width"), Read("Height"));
        if (bounds.Width <= 0 || bounds.Height <= 0) throw new InvalidDataException("Invalid saved node dimensions.");
        var ports = STNodeSavedState.ReadPortLayout(state.Properties);
        STNodePreviewAppearance appearance;
        if (node.Schema.NodeType == typeof(STMissingNode))
            appearance = new(ports?.Title ?? "?", Color.Goldenrod, "courier new", 8.25f, FontStyle.Regular, GraphicsUnit.Point, 26, 20, [], [], []);
        else
        {
            var defaults = (STNode)Activator.CreateInstance(node.Schema.NodeType, nonPublic: true)!;
            try
            {
                defaults.Create();
                appearance = defaults.CapturePreviewAppearance(state.Properties);
            }
            finally { if (defaults is IDisposable disposable) disposable.Dispose(); }
        }
        string title = state.Properties.TryGetValue("Title", out var titleBytes) ? Encoding.UTF8.GetString(titleBytes) : ports?.Title ?? appearance.Title;
        return new(node, state, bounds, appearance with { Title = title }, ports);
    }

    private static void DrawGrid(Graphics graphics, Size size, STNodeVisualTheme theme)
    {
        using var minor = new Pen(theme.GridMinor);
        using var major = new Pen(theme.GridMajor);
        for (int x = 0; x < size.Width; x += 20) graphics.DrawLine(x % 100 == 0 ? major : minor, x, 0, x, size.Height);
        for (int y = 0; y < size.Height; y += 20) graphics.DrawLine(y % 100 == 0 ? major : minor, 0, y, size.Width, y);
    }

    private static void DrawNode(Graphics graphics, Node node, FlowTemplateCoverStyle style, float scale)
    {
        var theme = style.Theme;
        var appearance = node.Appearance;
        Rectangle bounds = node.Bounds;
        using var outline = RoundedRectangle(bounds, style.CornerRadius);
        using var body = new SolidBrush(theme.Surface);
        using var title = new SolidBrush(theme.TitleSurface(appearance.TitleColor));
        using var text = new SolidBrush(theme.Text);
        using var font = new Font(appearance.FontName, appearance.FontSize, appearance.FontStyle, appearance.FontUnit);
        using var format = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center };
        var clip = graphics.Save();
        graphics.SetClip(outline, CombineMode.Intersect);
        graphics.FillPath(body, outline);
        graphics.FillRectangle(title, bounds.Left, bounds.Top, bounds.Width, appearance.TitleHeight);
        graphics.DrawString(appearance.Title, font, Brushes.White, new RectangleF(bounds.Left, bounds.Top + 2, bounds.Width, appearance.TitleHeight), format);
        graphics.Restore(clip);
        using var border = new Pen(theme.Border, 1 / scale);
        graphics.DrawPath(border, outline);

        if (node.Source.Inputs.Count(port => !port.Schema.IsEmpty) > 1)
        {
            format.Alignment = StringAlignment.Near;
            foreach (var input in node.Source.Inputs.Where(port => !port.Schema.IsEmpty))
            {
                string name = node.Ports?.Inputs.ElementAtOrDefault(input.LocalIndex)?.Name ?? input.Schema.Text;
                graphics.DrawString(name, font, text, new RectangleF(bounds.Left + 10,
                    bounds.Top + appearance.TitleHeight + input.LocalIndex * appearance.ItemHeight, Math.Max(0, bounds.Width - 20), appearance.ItemHeight), format);
            }
        }
        foreach (var control in appearance.Texts)
        {
            var rect = control.Bounds;
            rect.Offset(bounds.Left, bounds.Top + appearance.TitleHeight);
            var state = graphics.Save();
            graphics.SetClip(rect, CombineMode.Intersect);
            format.Alignment = control.Centered ? StringAlignment.Center : StringAlignment.Near;
            graphics.DrawString(control.Label, font, text, rect, format);
            format.Alignment = StringAlignment.Far;
            graphics.DrawString(control.Value, font, text, rect, format);
            graphics.Restore(state);
        }
        foreach (var port in node.Source.Inputs.Where(port => !port.Schema.IsEmpty)) DrawPort(graphics, node, true, port.LocalIndex, theme);
        foreach (var port in node.Source.Outputs.Where(port => !port.Schema.IsEmpty)) DrawPort(graphics, node, false, port.LocalIndex, theme);
    }

    private static RectangleF PortBounds(Node node, bool input, int index)
    {
        var defaults = (input ? node.Appearance.Inputs : node.Appearance.Outputs).ElementAtOrDefault(index);
        float size = Math.Max(1, defaults?.DotSize ?? 10);
        return new RectangleF((input ? node.Bounds.Left : node.Bounds.Right) - size / 2,
            node.Bounds.Top + node.Appearance.TitleHeight + index * node.Appearance.ItemHeight + (node.Appearance.ItemHeight - size) / 2f, size, size);
    }

    private static Color PortColor(Node node, bool input, int index, STNodeVisualTheme theme)
    {
        var defaults = (input ? node.Appearance.Inputs : node.Appearance.Outputs).ElementAtOrDefault(index);
        if (defaults?.DotColor is Color dot && dot.A != 0) return theme.ResolveAccent(dot);
        Type type = (input ? node.Source.Inputs : node.Source.Outputs)[index].DataType;
        Color color = type.Name == "CVStartCFC" ? Color.DarkGreen : type == typeof(bool) || type.Name == "CVLoopCFC" ? Color.DodgerBlue
            : type == typeof(string) ? Color.Yellow : type == typeof(int) ? Color.Cornsilk
            : type.Name is "MQActionEvent" or "CVMQTTRequest" or "CVBaseDataFlowResp" ? Color.DeepPink : Color.Gray;
        return theme.ResolveAccent(color);
    }

    private static void DrawPort(Graphics graphics, Node node, bool input, int index, STNodeVisualTheme theme)
    {
        var saved = (input ? node.Ports?.Inputs : node.Ports?.Outputs)?.ElementAtOrDefault(index);
        var defaults = (input ? node.Appearance.Inputs : node.Appearance.Outputs).ElementAtOrDefault(index);
        bool single = saved?.IsSingle ?? defaults?.IsSingle ?? input;
        var rect = PortBounds(node, input, index);
        using var brush = new SolidBrush(PortColor(node, input, index, theme));
        Type type = (input ? node.Source.Inputs : node.Source.Outputs)[index].DataType;
        if (type == typeof(object))
        {
            using var pen = new Pen(brush);
            if (single) graphics.DrawEllipse(pen, rect); else graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
        }
        else if (single) graphics.FillEllipse(brush, rect); else graphics.FillRectangle(brush, rect);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int diameter = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2) * 2;
        if (diameter <= 0) path.AddRectangle(bounds);
        else
        {
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
        }
        return path;
    }
}
