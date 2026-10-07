using ColorVision.Core;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;

public enum BmwSfrDisplayMetric
{
    [Description("MTF50（cycles/pixel）")] Mtf50,
    [Description("MTF10（cycles/pixel）")] Mtf10,
    [Description("指定频率 MTF（%）")] AtFrequency,
    [Description("Nyquist MTF@0.5（%）")] AtNyquist
}

public class BmwSfrOverlaySettings
{
    [Category("中心与区域"), DisplayName("显示靶标中心十字"), Description("只在定位成功后绘制识别出的中心，不使用搜索外框中心代替。")]
    public bool ShowTargetCenter { get; set; } = true;
    [Category("中心与区域"), DisplayName("显示中心坐标"), Description("显示原图像素坐标 X、Y；不随缩放变化。")]
    public bool ShowCenterCoordinates { get; set; }
    [Category("中心与区域"), DisplayName("显示测量框尺寸"), Description("显示每个内部 SFR 矩形的实际宽×高，单位为原图像素。")]
    public bool ShowRoiDimensions { get; set; }
    [Category("中心与区域"), DisplayName("显示距中心距离"), Description("显示各测量框中心到识别出的靶标中心的实际直线距离，单位为原图像素。")]
    public bool ShowCenterDistance { get; set; }
    [Category("结果图层"), DisplayName("显示点位名称")]
    public bool ShowPointNames { get; set; } = true;
    [Category("结果图层"), DisplayName("显示四边名称")]
    public bool ShowEdgeNames { get; set; } = true;
    [Category("结果图层"), DisplayName("显示指标数值"), Description("显示所选通道的指标；无效通道显示 INVALID，不以其他通道代替。")]
    public bool ShowValues { get; set; } = true;
    [Category("结果图层"), DisplayName("精简数值标注"), Description("只显示方向和数值，通道及指标名称由调用界面统一显示。")]
    public bool CompactMetricLabels { get; set; }
    [Category("结果图层"), DisplayName("刃边拟合虚线"), Description("在小矩形内显示当前通道实际拟合的刃边；没有拟合结果时不绘制。")]
    public bool ShowFittedEdges { get; set; } = true;
    [Category("指标"), DisplayName("显示指标")]
    public BmwSfrDisplayMetric Metric { get; set; } = BmwSfrDisplayMetric.Mtf50;
    [Category("指标"), DisplayName("显示频率 (cycles/pixel)"), Range(0, .5), Description("选择指定频率 MTF 时使用，例如 0.25；只查询已有曲线，不重新计算图像。")]
    public double Frequency { get; set; } = .25;
    [Category("文字"), DisplayName("固定屏幕字号"), Description("开启后缩放图像不会改变屏幕上的字号。")]
    public bool FixedScreenSize { get; set; } = true;
    [Category("文字"), DisplayName("文字字号"), Range(6, 128), Description("6～128，固定屏幕字号时为屏幕逻辑像素，否则为图像逻辑像素。")]
    public double FontSize { get; set; } = 12;
    public void Validate()
    {
        if (!double.IsFinite(FontSize) || FontSize < 6 || FontSize > 128) throw new ArgumentException("文字字号应为 6～128。");
        if (!Enum.IsDefined(Metric) || !double.IsFinite(Frequency) || Frequency < 0 || Frequency > .5) throw new ArgumentException("请选择有效指标，显示频率应为 0～0.5 cycles/pixel。");
    }
    public BmwSfrOverlaySettings Copy() => (BmwSfrOverlaySettings)MemberwiseClone();
}

/// <summary>Shared image annotation layout for production adjustment and the BMW result tool.</summary>
public static class BmwSfrOverlayRenderer
{
    public static DrawingVisual CreateVisual(BmwTargetAnalysis target, ImageSelectionScope scope, double zoom, string channel,
        BmwSfrOverlaySettings settings, BmwEdgeId? selectedEdge = null, bool drawSearch = true)
    {
        settings.Validate();
        double scale = double.IsFinite(zoom) && zoom > 0 ? zoom : 1;
        double textScale = settings.FixedScreenSize ? 1 / scale : 1;
        Point Position(double x, double y) => new(x * 96 / scope.DpiX, y * 96 / scope.DpiY);
        var visual = new DrawingVisual();
        using var dc = visual.RenderOpen();
        void Label(string text, Point anchor, Brush brush, string placement)
        {
            var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), settings.FontSize, brush, 1);
            formatted.MaxTextWidth = Math.Max(1, Math.Min(320 / scale, scope.CanvasWidth) / textScale - 8);
            formatted.Trimming = TextTrimming.CharacterEllipsis;
            double width = (formatted.Width + 8) * textScale, height = (formatted.Height + 4) * textScale, gap = 6 / scale;
            Point origin = placement switch
            {
                "left" => new(anchor.X - width - gap, anchor.Y - height / 2),
                "right" => new(anchor.X + gap, anchor.Y - height / 2),
                "top" => new(anchor.X - width / 2, anchor.Y - height - gap),
                "bottom" => new(anchor.X - width / 2, anchor.Y + gap),
                _ => anchor
            };
            origin.X = Math.Clamp(origin.X, 0, Math.Max(0, scope.CanvasWidth - width));
            origin.Y = Math.Clamp(origin.Y, 0, Math.Max(0, scope.CanvasHeight - height));
            dc.PushTransform(new MatrixTransform(textScale, 0, 0, textScale, origin.X, origin.Y));
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(230, 20, 24, 30)), null, new Rect(0, 0, formatted.Width + 8, formatted.Height + 4), 3, 3);
            dc.DrawText(formatted, new Point(4, 2));
            dc.Pop();
        }
        var search = target.SearchRoi;
        if (drawSearch && search.Width > 0 && search.Height > 0)
            dc.DrawRectangle(null, new Pen(Brushes.Red, 1.5 / scale), new Rect(Position(search.X, search.Y), Position(search.X + search.Width, search.Y + search.Height)));
        if (settings.ShowPointNames) Label(target.Id, Position(search.X, search.Y), Brushes.White, "name");
        if (BmwSfrPresentation.HasCenter(target))
        {
            var center = Position(target.CenterX, target.CenterY);
            if (settings.ShowTargetCenter)
            {
                double arm = 6 / scale;
                var pen = new Pen(Brushes.Red, 1.5 / scale);
                dc.DrawLine(pen, new(center.X - arm, center.Y), new(center.X + arm, center.Y));
                dc.DrawLine(pen, new(center.X, center.Y - arm), new(center.X, center.Y + arm));
            }
            if (settings.ShowCenterCoordinates)
                Label($"中心 X {target.CenterX:F1}  Y {target.CenterY:F1} px", center + new Vector(9 / scale, 9 / scale), Brushes.White, "center");
        }
        foreach (var edge in target.Edges)
        {
            var r = edge.Roi;
            if (r.Width <= 0 || r.Height <= 0) continue;
            bool valid = BmwSfrPresentation.Channel(edge, channel) is { Valid: true };
            bool selected = selectedEdge == edge.Id;
            Brush color = selected ? Brushes.DeepSkyBlue : valid ? Brushes.LightGreen : Brushes.Orange;
            dc.DrawRectangle(selected ? new SolidColorBrush(Color.FromArgb(35, 30, 170, 255)) : null,
                new Pen(selected ? Brushes.DeepSkyBlue : Brushes.Red, (selected ? 2.5 : 1.5) / scale), new Rect(Position(r.X, r.Y), Position(r.X + r.Width, r.Y + r.Height)));
            if (settings.ShowFittedEdges && BmwSfrPresentation.Channel(edge, channel) is { FitAvailable: true } fit
                && double.IsFinite(fit.EdgeSlope) && double.IsFinite(fit.EdgeIntercept))
            {
                double extent = (fit.Rotated ? r.Width : r.Height) - 1;
                double start = fit.EdgeIntercept, end = start + fit.EdgeSlope * extent;
                Point a = fit.Rotated ? Position(r.X + r.Width - 1, r.Y + start) : Position(r.X + start, r.Y);
                Point b = fit.Rotated ? Position(r.X, r.Y + end) : Position(r.X + end, r.Y + r.Height - 1);
                dc.PushClip(new RectangleGeometry(new Rect(Position(r.X, r.Y), Position(r.X + r.Width, r.Y + r.Height))));
                dc.DrawLine(new Pen(Brushes.Yellow, 1.5 / scale) { DashStyle = DashStyles.Dash }, a, b);
                dc.Pop();
            }
            string label = BmwSfrPresentation.OverlayLabel(edge, channel, settings, target);
            if (label.Length == 0) continue;
            Point anchor = edge.Id switch
            {
                BmwEdgeId.Left => Position(search.X, r.Y + r.Height / 2.0),
                BmwEdgeId.Right => Position(search.X + search.Width, r.Y + r.Height / 2.0),
                BmwEdgeId.Top => Position(r.X + r.Width / 2.0, search.Y),
                _ => Position(r.X + r.Width / 2.0, search.Y + search.Height)
            };
            Label(label, anchor, color, edge.Id.ToString().ToLowerInvariant());
        }
        return visual;
    }
}
