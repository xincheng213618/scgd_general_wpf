using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    ///
    /// Two <see cref="DrawingVisual"/> children are used:
    /// <list type="bullet">
    ///   <item><description><c>_contentVisual</c> – the full treemap, rebuilt only when data
    ///     or control size changes.</description></item>
    ///   <item><description><c>_overlayVisual</c> – a single hover-highlight rectangle,
    ///     rebuilt cheaply on every mouse-move when the hovered node changes.</description></item>
    /// </list>
    /// This separation ensures that moving the mouse over thousands of nodes does not
    /// trigger an expensive full redraw.
    /// </summary>
    public class TreemapControl : FrameworkElement
    {
        // ─── Dependency Properties ────────────────────────────────────────────

        public static readonly DependencyProperty RootNodeProperty =
            DependencyProperty.Register(nameof(RootNode), typeof(TreemapNode), typeof(TreemapControl),
                new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.AffectsMeasure,
                    OnRootNodeChanged));

        public static readonly DependencyProperty ShowLabelsProperty =
            DependencyProperty.Register(nameof(ShowLabels), typeof(bool), typeof(TreemapControl),
                new FrameworkPropertyMetadata(true, OnShowLabelsChanged));

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
            var ctrl = (TreemapControl)d;
            ctrl._hoveredNode = null;
            ctrl.RenderContent();
            ctrl.RenderOverlay();
        }

        private static void OnShowLabelsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreemapControl)d).RenderContent();
        }

        // ─── Two DrawingVisuals (static content + dynamic hover overlay) ──────

        private readonly DrawingVisual _contentVisual = new DrawingVisual();
        private readonly DrawingVisual _overlayVisual = new DrawingVisual();

        protected override int VisualChildrenCount => 2;
        protected override Visual GetVisualChild(int index) =>
            index == 0 ? _contentVisual : _overlayVisual;

        // ─── Events ───────────────────────────────────────────────────────────

        /// <summary>Fired when the user right-clicks a node.</summary>
        public event EventHandler<TreemapNodeEventArgs>? NodeRightClicked;

        /// <summary>Fired when the user left-clicks a node.</summary>
        public event EventHandler<TreemapNodeEventArgs>? NodeClicked;

        // ─── Hover state ──────────────────────────────────────────────────────

        private TreemapNode? _hoveredNode;

        // ─── Mouse-tracking tooltip (Popup tracks cursor; ToolTip does not) ──────

        private readonly Popup _hoverPopup;
        private readonly TextBlock _hoverText;

        // ─── Construction ─────────────────────────────────────────────────────

        public TreemapControl()
        {
            AddVisualChild(_contentVisual);
            AddLogicalChild(_contentVisual);
            AddVisualChild(_overlayVisual);
            AddLogicalChild(_overlayVisual);

            // Build a floating popup for hover info that follows the mouse cursor.
            // WPF's built-in ToolTip only repositions when it is first opened; the
            // Popup approach updates HorizontalOffset/VerticalOffset every MouseMove.
            _hoverText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 11,
                LineHeight = 17,
            };
            _hoverPopup = new Popup
            {
                Child = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(230, 28, 28, 28)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(100, 180, 180, 180)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8, 5, 8, 5),
                    CornerRadius = new CornerRadius(3),
                    Child = _hoverText,
                },
                Placement = PlacementMode.AbsolutePoint,
                AllowsTransparency = true,
                IsHitTestVisible = false,
                StaysOpen = true,
                PlacementTarget = this,
            };

            MouseMove += OnMouseMove;
            MouseLeave += OnMouseLeave;
            MouseRightButtonUp += OnMouseRightButtonUp;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
        }

        // ─── Layout engine ────────────────────────────────────────────────────

        private readonly TreemapLayout _layout = new TreemapLayout();

        // ─── DPI (cached to avoid per-render lookup; updated on monitor change) ─

        private double _pixelsPerDip = 1.0;

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            _pixelsPerDip = newDpi.PixelsPerDip;
            RenderContent();
        }

        // ─── Colour palette (12 hues, assigned round-robin to root's children) ─

        private static readonly Color[] Palette =
        {
            Color.FromRgb(0x4E, 0x79, 0xA7),
            Color.FromRgb(0x59, 0xA1, 0x4F),
            Color.FromRgb(0xF2, 0x8E, 0x2B),
            Color.FromRgb(0xE1, 0x57, 0x59),
            Color.FromRgb(0x76, 0xB7, 0xB2),
            Color.FromRgb(0xB0, 0x7A, 0xA1),
            Color.FromRgb(0xBA, 0xB0, 0xAC),   // neutral grey-taupe (replaces duplicate green at index 1)
            Color.FromRgb(0xD3, 0x7C, 0x2D),
            Color.FromRgb(0x8C, 0xD1, 0x7D),
            Color.FromRgb(0x9C, 0x75, 0x5F),
            Color.FromRgb(0x49, 0x9C, 0xD5),
            Color.FromRgb(0xFF, 0x9D, 0xA7),
        };

        /// <summary>
        /// Per-node base colour, inherited from the top-level ancestor's palette slot.
        /// All files/folders under the same root-level folder share a base hue.
        /// </summary>
        private readonly Dictionary<TreemapNode, Color> _nodeColors = new Dictionary<TreemapNode, Color>();

        /// <summary>
        /// Brush cache keyed by packed ARGB, so the same colour never allocates
        /// more than one frozen <see cref="SolidColorBrush"/>.
        /// </summary>
        private readonly Dictionary<uint, SolidColorBrush> _brushCache =
            new Dictionary<uint, SolidColorBrush>();

        private SolidColorBrush GetBrush(Color c)
        {
            uint key = ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
            if (!_brushCache.TryGetValue(key, out var brush))
            {
                brush = new SolidColorBrush(c);
                brush.Freeze();
                _brushCache[key] = brush;
            }
            return brush;
        }

        // ─── Render: full content (called on data / size change only) ─────────

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            RenderContent();
            RenderOverlay();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            // Called for the initial paint.  If content has already been rendered
            // (e.g. by OnRenderSizeChanged) this is a no-op to avoid double work.
            if (_layout.RenderOrder.Count == 0)
            {
                RenderContent();
                RenderOverlay();
            }
        }

        private void RenderContent()
        {
            using DrawingContext dc = _contentVisual.RenderOpen();

            // Background
            dc.DrawRectangle(GetBrush(Color.FromRgb(0x1e, 0x1e, 0x1e)), null,
                new Rect(0, 0, ActualWidth, ActualHeight));

            TreemapNode? root = RootNode;
            if (root == null || ActualWidth <= 0 || ActualHeight <= 0) return;

            _layout.Calculate(root, new Rect(0, 0, ActualWidth, ActualHeight));
            if (_layout.RenderOrder.Count == 0) return;

            // Assign base colours via inheritance: each root child gets a distinct
            // palette colour; all descendants share that ancestor's hue.
            _nodeColors.Clear();
            int paletteIdx = 0;
            AssignColors(root, null, ref paletteIdx);

            var folderBorderPen = new Pen(GetBrush(Color.FromArgb(160, 0, 0, 0)), 1.0);
            folderBorderPen.Freeze();
            var fileBorderPen = new Pen(GetBrush(Color.FromArgb(60, 0, 0, 0)), 0.5);
            fileBorderPen.Freeze();

            var typeface = new Typeface("Segoe UI");
            double dpi = _pixelsPerDip;

            // ── Pass 1: draws (in parent-before-child order):
            //   • node fill + border
            //   • folder header-band background
            //   • file labels (leaves have no children drawn on top)
            // Folder labels are intentionally NOT drawn here so that they don't get
            // covered by child rectangles drawn in subsequent iterations.
            foreach (var (node, rect) in _layout.RenderOrder)
            {
                if (rect.Width < 2 || rect.Height < 2) continue;

                bool isFolder = !node.IsLeaf;
                Color baseColor = GetBaseColor(node);
                Color fillColor = isFolder ? baseColor : Darken(baseColor, 0.22f);
                dc.DrawRectangle(GetBrush(fillColor), isFolder ? folderBorderPen : fileBorderPen, rect);

                if (isFolder)
                {
                    // Header-band background — darker strip reserved by the layout.
                    if (rect.Height >= TreemapLayout.FolderHeaderHeight + 4)
                    {
                        var hdrRect = new Rect(rect.X, rect.Y, rect.Width, TreemapLayout.FolderHeaderHeight);
                        dc.DrawRectangle(GetBrush(Darken(fillColor, 0.30f)), null, hdrRect);
                    }
                    // Folder label drawn in Pass 2 so it appears on top of children.
                }
                else if (ShowLabels && rect.Width >= 32 && rect.Height >= 12)
                {
                    // Leaf file label: nothing will be drawn on top of it.
                    double fsize = Math.Max(7.0, Math.Min(rect.Height * 0.32, rect.Width / 7.0));
                    fsize = Math.Min(fsize, 11.0);
                    var ft = new FormattedText(node.Name, CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, typeface, fsize, Brushes.White, dpi);
                    ft.MaxTextWidth = Math.Max(1, rect.Width - 4);
                    ft.MaxTextHeight = rect.Height - 2;
                    ft.Trimming = TextTrimming.CharacterEllipsis;
                    dc.DrawText(ft, new Point(
                        rect.X + 2,
                        rect.Y + (rect.Height - ft.Height) / 2));
                }
            }

            if (!ShowLabels) return;

            // ── Pass 2: draw folder labels on top of all children.
            // Because this runs AFTER Pass 1 (which drew all children), the text
            // rendered here always appears on top regardless of how deep the folder is.
            foreach (var (node, rect) in _layout.RenderOrder)
            {
                if (node.IsLeaf || rect.Width < 24 || rect.Height < 10) continue;

                bool hasHeaderSpace = rect.Height >= TreemapLayout.FolderHeaderHeight + 4;

                double labelAreaY, labelAreaH;
                if (hasHeaderSpace)
                {
                    // Use the dedicated header band reserved by the layout.
                    labelAreaY = rect.Y;
                    labelAreaH = TreemapLayout.FolderHeaderHeight;
                }
                else
                {
                    // Small folder: use whatever vertical space is available at the top.
                    labelAreaY = rect.Y + 1;
                    labelAreaH = Math.Min(rect.Height - 2, 12.0);
                }

                // For wider folders, append the size to the name.
                string text = (hasHeaderSpace && rect.Width >= 80)
                    ? $"{node.Name}  {FormatSize(node.Size)}"
                    : node.Name;
                double fsize = Math.Max(7.0, Math.Min(labelAreaH - 2.0, 11.0));

                var ft = new FormattedText(text, CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typeface, fsize, Brushes.White, dpi);
                ft.MaxTextWidth = Math.Max(1, rect.Width - 6);
                ft.MaxTextHeight = labelAreaH;
                ft.Trimming = TextTrimming.CharacterEllipsis;
                dc.DrawText(ft, new Point(
                    rect.X + 3,
                    labelAreaY + (labelAreaH - ft.Height) / 2));
            }
        }

        // ─── Render: hover overlay (called cheaply on every hover change) ─────

        private void RenderOverlay()
        {
            using DrawingContext dc = _overlayVisual.RenderOpen();
            if (_hoveredNode != null &&
                _layout.LayoutResult.TryGetValue(_hoveredNode, out Rect r) &&
                r.Width > 2 && r.Height > 2)
            {
                var brush = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255));
                brush.Freeze();
                var pen = new Pen(Brushes.White, 2.0);
                pen.Freeze();
                dc.DrawRectangle(brush, pen, r);
            }
            // Opening and closing the context with nothing drawn clears the overlay.
        }

        // ─── Colour assignment ────────────────────────────────────────────────

        /// <summary>
        /// Assigns base colours recursively.  Root children each get a fresh palette
        /// colour; all descendants inherit the ancestor's colour unchanged.
        /// </summary>
        private void AssignColors(TreemapNode node, Color? inherited, ref int paletteIdx)
        {
            foreach (var child in node.Children)
            {
                Color color = inherited ?? Palette[paletteIdx++ % Palette.Length];
                _nodeColors[child] = color;
                if (!child.IsLeaf)
                    AssignColors(child, color, ref paletteIdx);
            }
        }

        private Color GetBaseColor(TreemapNode node) =>
            _nodeColors.TryGetValue(node, out Color c) ? c : Palette[0];

        private static Color Darken(Color c, float amount) =>
            Color.FromRgb(
                (byte)Math.Max(0, c.R - (int)(c.R * amount)),
                (byte)Math.Max(0, c.G - (int)(c.G * amount)),
                (byte)Math.Max(0, c.B - (int)(c.B * amount)));

        // ─── Mouse: hover, click, right-click ─────────────────────────────────

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            Point pos = e.GetPosition(this);
            TreemapNode? hit = HitTest(pos);

            // Only redraw the overlay when the hovered node actually changes.
            if (!ReferenceEquals(hit, _hoveredNode))
            {
                _hoveredNode = hit;
                RenderOverlay();  // Fast: draws at most one rectangle.
            }

            // Update the floating popup to follow the mouse cursor.
            // Using a Popup with PlacementMode.AbsolutePoint lets us update the
            // screen position on every MouseMove, unlike the WPF ToolTip which
            // keeps the position from when it was first opened.
            if (hit != null)
            {
                try
                {
                    Point screenPos = PointToScreen(pos);
                    _hoverPopup.HorizontalOffset = screenPos.X + 15;
                    _hoverPopup.VerticalOffset = screenPos.Y + 15;

                    string typeStr = hit.IsLeaf ? "文件" : "文件夹";
                    string sizeStr = FormatSize(hit.Size);
                    string pathLine = hit.FullPath != null ? $"\n{hit.FullPath}" : string.Empty;
                    _hoverText.Text = $"{hit.Name}\n{typeStr}  {sizeStr}{pathLine}";

                    if (!_hoverPopup.IsOpen)
                        _hoverPopup.IsOpen = true;
                }
                catch { /* PointToScreen can fail before element is visible */ }
            }
            else
            {
                _hoverPopup.IsOpen = false;
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            _hoverPopup.IsOpen = false;
            if (_hoveredNode != null)
            {
                _hoveredNode = null;
                RenderOverlay();
            }
        }

        private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(this);
            TreemapNode? hit = HitTest(pos);
            if (hit != null)
            {
                NodeRightClicked?.Invoke(this, new TreemapNodeEventArgs(hit, PointToScreen(pos)));
                e.Handled = true;
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(this);
            TreemapNode? hit = HitTest(pos);
            if (hit != null)
            {
                NodeClicked?.Invoke(this, new TreemapNodeEventArgs(hit, PointToScreen(pos)));
                e.Handled = true;
            }
        }

        // ─── Hit-test ─────────────────────────────────────────────────────────

        private TreemapNode? HitTest(Point p)
        {
            // Find the smallest rect containing p — that is the deepest (leaf-most) node.
            TreemapNode? best = null;
            double bestArea = double.MaxValue;

            foreach (var (node, rect) in _layout.LayoutResult)
            {
                if (rect.Contains(p))
                {
                    double area = rect.Width * rect.Height;
                    if (area < bestArea)
                    {
                        best = node;
                        bestArea = area;
                    }
                }
            }
            return best;
        }

        // ─── Utilities ────────────────────────────────────────────────────────

        internal static string FormatSize(double bytes)
        {
            if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
            if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes:F0} B";
        }
    }
}
