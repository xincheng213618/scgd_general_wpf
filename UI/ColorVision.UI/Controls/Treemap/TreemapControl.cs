using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI.Controls
{
    /// <summary>Event args carrying the node that was clicked plus the mouse position.</summary>
    public class TreemapNodeEventArgs : EventArgs
    {
        public TreemapNode Node { get; }
        public Point ScreenPosition { get; }
        public TreemapNodeEventArgs(TreemapNode node, Point screenPosition)
        {
            Node = node;
            ScreenPosition = screenPosition;
        }
    }

    /// <summary>
    /// High-performance WPF Treemap control.
    /// Uses <see cref="DrawingVisual"/> / <see cref="DrawingContext"/> for rendering
    /// so it can display thousands of rectangles efficiently.
    /// </summary>
    public class TreemapControl : FrameworkElement
    {
        // ─── Dependency Properties ────────────────────────────────────────────

        public static readonly DependencyProperty RootNodeProperty =
            DependencyProperty.Register(nameof(RootNode), typeof(TreemapNode), typeof(TreemapControl),
                new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.AffectsMeasure |
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnRootNodeChanged));

        public static readonly DependencyProperty ShowLabelsProperty =
            DependencyProperty.Register(nameof(ShowLabels), typeof(bool), typeof(TreemapControl),
                new FrameworkPropertyMetadata(true,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>Root of the data hierarchy to visualise.</summary>
        public TreemapNode? RootNode
        {
            get => (TreemapNode?)GetValue(RootNodeProperty);
            set => SetValue(RootNodeProperty, value);
        }

        /// <summary>Whether to draw text labels inside large enough nodes.</summary>
        public bool ShowLabels
        {
            get => (bool)GetValue(ShowLabelsProperty);
            set => SetValue(ShowLabelsProperty, value);
        }

        private static void OnRootNodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreemapControl)d).InvalidateVisual();
        }

        // ─── Visual tree (single DrawingVisual child) ─────────────────────────

        private readonly DrawingVisual _drawingVisual = new DrawingVisual();

        // ─── Events ───────────────────────────────────────────────────────────

        /// <summary>Fired when the user right-clicks a node.</summary>
        public event EventHandler<TreemapNodeEventArgs>? NodeRightClicked;

        /// <summary>Fired when the user left-clicks a node.</summary>
        public event EventHandler<TreemapNodeEventArgs>? NodeClicked;

        // ─── Hover/selection state ────────────────────────────────────────────

        private TreemapNode? _hoveredNode;

        public TreemapControl()
        {
            AddVisualChild(_drawingVisual);
            AddLogicalChild(_drawingVisual);

            // Tooltip setup
            ToolTip = new ToolTip { Content = string.Empty };
            ToolTipService.SetInitialShowDelay(this, 200);
            MouseMove += OnMouseMove;
            MouseLeave += OnMouseLeave;
            MouseRightButtonUp += OnMouseRightButtonUp;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
        }

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => _drawingVisual;

        // ─── Layout ───────────────────────────────────────────────────────────

        private readonly TreemapLayout _layout = new TreemapLayout();

        // Palette – 12 distinct hues used round-robin per depth level
        private static readonly Color[] Palette =
        {
            Color.FromRgb(0x4E, 0x79, 0xA7),
            Color.FromRgb(0xF2, 0x8E, 0x2B),
            Color.FromRgb(0x59, 0xA1, 0x4F),
            Color.FromRgb(0xE1, 0x57, 0x59),
            Color.FromRgb(0x76, 0xB7, 0xB2),
            Color.FromRgb(0xFF, 0x9D, 0xA7),
            Color.FromRgb(0x9C, 0x75, 0x5F),
            Color.FromRgb(0xBA, 0xB0, 0xAC),
            Color.FromRgb(0x8C, 0xD1, 0x7D),
            Color.FromRgb(0xB0, 0x7A, 0xA1),
            Color.FromRgb(0xD3, 0x7C, 0x2D),
            Color.FromRgb(0x49, 0x9C, 0xD5),
        };

        /// <summary>
        /// Per-depth offset applied to the colour index so that adjacent depth levels
        /// use visually distinct hues from the <see cref="Palette"/> array.
        /// </summary>
        private const int ColorDepthMultiplier = 3;

        // Mapping node→colour index (filled during render)
        private readonly Dictionary<TreemapNode, int> _colorIndex =
            new Dictionary<TreemapNode, int>();

        // ─── Rendering ────────────────────────────────────────────────────────

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            Render();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            Render();
        }

        private void Render()
        {
            using DrawingContext dc = _drawingVisual.RenderOpen();

            // Background
            dc.DrawRectangle(Brushes.DarkGray, null,
                new Rect(0, 0, ActualWidth, ActualHeight));

            TreemapNode? root = RootNode;
            if (root == null || ActualWidth <= 0 || ActualHeight <= 0) return;

            // Recalculate layout
            Rect bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            _layout.Calculate(root, bounds);

            if (_layout.LayoutResult.Count == 0) return;

            _colorIndex.Clear();

            // Assign colour indices to root's children (round-robin)
            AssignColors(root, 0);

            // Render every laid-out node
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)), 0.5);
            borderPen.Freeze();
            var hoverPen = new Pen(Brushes.White, 2.0);
            hoverPen.Freeze();

            var typeface = new Typeface("Segoe UI");

            foreach (var (node, rect) in _layout.LayoutResult)
            {
                if (rect.Width < 2 || rect.Height < 2) continue;

                // Fill colour
                Color baseColor = node.Color ?? GetNodeColor(node);
                // Lighten on hover
                if (ReferenceEquals(node, _hoveredNode))
                    baseColor = Lighten(baseColor, 0.25f);
                var brush = new SolidColorBrush(baseColor);
                brush.Freeze();

                var pen = ReferenceEquals(node, _hoveredNode) ? hoverPen : borderPen;
                dc.DrawRectangle(brush, pen, rect);

                // Label: only if rectangle is large enough
                if (ShowLabels && rect.Width >= 32 && rect.Height >= 14)
                {
                    string text = node.Name;
                    double fontSize = Math.Min(11.0, rect.Height * 0.35);
                    fontSize = Math.Max(fontSize, 7.0);

                    var ft = new FormattedText(
                        text,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        fontSize,
                        Brushes.White,
                        VisualTreeHelper.GetDpi(_drawingVisual).PixelsPerDip);

                    ft.MaxTextWidth = Math.Max(1, rect.Width - 4);
                    ft.MaxTextHeight = rect.Height - 2;
                    ft.Trimming = TextTrimming.CharacterEllipsis;

                    double tx = rect.X + 2;
                    double ty = rect.Y + (rect.Height - ft.Height) / 2;
                    dc.DrawText(ft, new Point(tx, ty));
                }
            }
        }

        private void AssignColors(TreemapNode node, int depth)
        {
            for (int i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                _colorIndex[child] = (depth * ColorDepthMultiplier + i) % Palette.Length;
                if (!child.IsLeaf)
                    AssignColors(child, depth + 1);
            }
        }

        private Color GetNodeColor(TreemapNode node)
        {
            if (_colorIndex.TryGetValue(node, out int idx))
            {
                // Slightly darken leaf nodes
                Color c = Palette[idx % Palette.Length];
                if (node.IsLeaf)
                    c = Darken(c, 0.15f);
                return c;
            }
            return Palette[0];
        }

        private static Color Darken(Color c, float amount)
        {
            return Color.FromRgb(
                (byte)Math.Max(0, c.R - (int)(c.R * amount)),
                (byte)Math.Max(0, c.G - (int)(c.G * amount)),
                (byte)Math.Max(0, c.B - (int)(c.B * amount)));
        }

        private static Color Lighten(Color c, float amount)
        {
            return Color.FromRgb(
                (byte)Math.Min(255, c.R + (int)((255 - c.R) * amount)),
                (byte)Math.Min(255, c.G + (int)((255 - c.G) * amount)),
                (byte)Math.Min(255, c.B + (int)((255 - c.B) * amount)));
        }

        // ─── Hit-test & Tooltip ───────────────────────────────────────────────

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            Point pos = e.GetPosition(this);
            TreemapNode? hit = HitTest(pos);

            // Update hover highlight only if it changed
            if (!ReferenceEquals(hit, _hoveredNode))
            {
                _hoveredNode = hit;
                Render();
            }

            if (ToolTip is ToolTip tt)
            {
                if (hit != null)
                {
                    string sizeStr = FormatSize(hit.Size);
                    string pathInfo = hit.FullPath != null ? $"\n{hit.FullPath}" : string.Empty;
                    tt.Content = $"{hit.Name}  {sizeStr}{pathInfo}";
                    tt.IsOpen = true;
                }
                else
                {
                    tt.IsOpen = false;
                }
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (ToolTip is ToolTip tt)
                tt.IsOpen = false;
            if (_hoveredNode != null)
            {
                _hoveredNode = null;
                Render();
            }
        }

        private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(this);
            TreemapNode? hit = HitTest(pos);
            if (hit != null)
            {
                Point screen = PointToScreen(pos);
                NodeRightClicked?.Invoke(this, new TreemapNodeEventArgs(hit, screen));
                e.Handled = true;
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(this);
            TreemapNode? hit = HitTest(pos);
            if (hit != null)
            {
                Point screen = PointToScreen(pos);
                NodeClicked?.Invoke(this, new TreemapNodeEventArgs(hit, screen));
                e.Handled = true;
            }
        }

        private TreemapNode? HitTest(Point p)
        {
            TreemapNode? best = null;
            double bestArea = double.MaxValue;

            foreach (var (node, rect) in _layout.LayoutResult)
            {
                if (rect.Contains(p) && rect.Width * rect.Height < bestArea)
                {
                    best = node;
                    bestArea = rect.Width * rect.Height;
                }
            }
            return best;
        }

        private static string FormatSize(double bytes)
        {
            if (bytes >= 1073741824)
                return $"{bytes / 1073741824.0:F1} GB";
            if (bytes >= 1048576)
                return $"{bytes / 1048576.0:F1} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024.0:F1} KB";
            return $"{bytes:F0} B";
        }
    }
}
