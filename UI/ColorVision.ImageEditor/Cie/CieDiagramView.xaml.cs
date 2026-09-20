using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.Cie
{
    public sealed record CieDiagramSegment(CieChromaticity Start, CieChromaticity End, Color Color, bool Dashed = false);

    public partial class CieDiagramView : System.Windows.Controls.UserControl
    {
        private static readonly DependencyProperty ThemeBackgroundProperty = DependencyProperty.Register(
            "ThemeBackground", typeof(Brush), typeof(CieDiagramView),
            new FrameworkPropertyMetadata(Brushes.White, (d, _) => ((CieDiagramView)d).RefreshTheme()));
        private bool _dark;
        private readonly CieOverlayVisual _overlayVisual = new();
        private readonly DrawingVisual _segmentVisual = new();
        private readonly List<CieDiagramSegment> _segments = new();
        private readonly List<CieGamut> _gamuts = new();
        private readonly List<CieMarker> _markers = new();
        private readonly List<CieMarker> _referenceMarkers = new();
        private CieDiagramProfile _profile = CieDiagramProfiles.Cie1931xy;
        private BitmapSource? _background;
        private CieMarker? _selectedMarker;
        private CieChromaticity _cursorXy = CieChromaticity.Empty;
        public CieChromaticity ReferenceWhite { get; private set; } = CieIlluminants.D65.Chromaticity;
        private bool _showCctReference = true;
        private bool _showDaylightReference = true;
        private bool _autoFit = true;
        private bool _isFitting;
        private DispatcherOperation? _pendingFit;

        public CieDiagramView()
        {
            InitializeComponent();
            SetResourceReference(ThemeBackgroundProperty, "GlobalBackground");
            RefreshTheme();

            Loaded += CieDiagramView_Loaded;
            Unloaded += CieDiagramView_Unloaded;
            ZoomBox.SizeChanged += (_, _) => QueueFit();
            DiagramCanvas.SizeChanged += DiagramCanvas_SizeChanged;
            DiagramCanvas.MouseLeave += DiagramCanvas_MouseLeave;
            DiagramCanvas.MouseMove += DiagramCanvas_MouseMove;
            DiagramCanvas.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount != 2) return;
                CieChromaticity xy = GetChromaticityAt(e.GetPosition(DiagramCanvas));
                if (xy.IsFinite) { PointPicked?.Invoke(this, xy); e.Handled = true; }
            };
            ZoomBox.ContentMatrixChanged += ZoomBox_ContentMatrixChanged;

            SetDiagram(CieDiagramKind.Cie1931xy);
        }

        public event EventHandler<string>? CursorTextChanged;
        public event EventHandler<CieChromaticity>? PointPicked;

        public void SetReferenceWhite(CieChromaticity white)
        {
            if (!white.IsFinite || white.X <= 0 || white.Y <= 0 || white.X + white.Y >= 1)
                throw new ArgumentException("参考白色坐标必须满足 x > 0、y > 0、x + y < 1。");
            if (ReferenceWhite == white) return;
            ReferenceWhite = white;
            if (_cursorXy.IsFinite) CursorTextChanged?.Invoke(this, new CiePointReadout(_cursorXy, white).CursorText);
        }

        public CieDiagramKind DiagramKind => _profile.Kind;

        public CieDiagramProfile Profile => _profile;

        public IReadOnlyList<CieGamut> Gamuts => _gamuts;

        public IReadOnlyList<CieMarker> Markers => _markers;

        public IReadOnlyList<CieMarker> ReferenceMarkers => _referenceMarkers;

        public void SetSegments(IEnumerable<CieDiagramSegment> segments)
        {
            _segments.Clear();
            _segments.AddRange(segments);
            RenderOverlay();
        }

        public CieChromaticity GetChromaticityAt(Point canvasPoint)
        {
            if (_background == null || DiagramCanvas.ActualWidth <= 0 || DiagramCanvas.ActualHeight <= 0)
                return CieChromaticity.Empty;
            Point pixel = new(canvasPoint.X / DiagramCanvas.ActualWidth * _background.PixelWidth,
                canvasPoint.Y / DiagramCanvas.ActualHeight * _background.PixelHeight);
            CieChromaticity point = _profile.ImagePixelToDiagramPoint(pixel);
            return _profile.ContainsDiagramPoint(point) ? _profile.FromDiagramPoint(point) : CieChromaticity.Empty;
        }

        public bool ShowCctReference
        {
            get => _showCctReference;
            set
            {
                if (_showCctReference == value)
                {
                    return;
                }

                _showCctReference = value;
                RenderOverlay();
            }
        }

        public bool ShowDaylightReference
        {
            get => _showDaylightReference;
            set
            {
                if (_showDaylightReference == value)
                {
                    return;
                }

                _showDaylightReference = value;
                RenderOverlay();
            }
        }

        public void SetDiagram(CieDiagramKind kind)
        {
            _profile = CieDiagramProfiles.Get(kind);
            _background = LoadBackground(_profile, _dark);
            DiagramCanvas.Source = _background;
            EnsureOverlayVisual();
            RenderOverlay();

            ZoomUniform();
        }

        public void SetGamuts(IEnumerable<CieGamut> gamuts)
        {
            _gamuts.Clear();
            _gamuts.AddRange(gamuts);
            RenderOverlay();
        }

        public void AddGamut(CieGamut gamut)
        {
            if (_gamuts.Any(item => string.Equals(item.Name, gamut.Name, StringComparison.Ordinal)))
            {
                return;
            }

            _gamuts.Add(gamut);
            RenderOverlay();
        }

        public void RemoveGamut(string name)
        {
            _gamuts.RemoveAll(item => string.Equals(item.Name, name, StringComparison.Ordinal));
            RenderOverlay();
        }

        public void ClearGamuts()
        {
            _gamuts.Clear();
            RenderOverlay();
        }

        public void SetMarkers(IEnumerable<CieMarker> markers)
        {
            _markers.Clear();
            _markers.AddRange(markers);
            RenderOverlay();
        }

        public void SetReferenceMarkers(IEnumerable<CieMarker> markers)
        {
            _referenceMarkers.Clear();
            _referenceMarkers.AddRange(markers);
            RenderOverlay();
        }

        public void AddMarker(CieMarker marker)
        {
            _markers.Add(marker);
            RenderOverlay();
        }

        public void ClearMarkers()
        {
            _markers.Clear();
            RenderOverlay();
        }

        public void SetSelectedXy(double x, double y)
        {
            SetSelectedXy(new CieChromaticity(x, y), Colors.Black, "Current");
        }

        public void SetSelectedXy(CieChromaticity xy, Color color, string name = "Current")
        {
            _selectedMarker = xy.IsFinite ? new CieMarker(name, xy, color) : null;
            RenderOverlay();
        }

        public void SetSelectedRgb(int r, int g, int b)
        {
            CieChromaticity xy = CieColorConverter.RgbToCie1931xy(r, g, b);
            SetSelectedXy(xy, CieColorConverter.ToMarkerColor(r, g, b), "RGB");
        }

        public void ClearSelection()
        {
            _selectedMarker = null;
            RenderOverlay();
        }

        public void ZoomUniform()
        {
            _autoFit = true;
            QueueFit();
        }

        public void Zoom(double factor) => ZoomBox.Zoom(factor);

        private void QueueFit()
        {
            if (!_autoFit || !IsLoaded || !IsVisible || _pendingFit?.Status == DispatcherOperationStatus.Pending)
                return;
            _pendingFit = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                _pendingFit = null;
                if (!_autoFit || !IsLoaded || !IsVisible || ZoomBox.ActualWidth <= 0 || ZoomBox.ActualHeight <= 0)
                    return;
                ZoomBox.UpdateLayout();
                if (!DiagramCanvas.IsArrangeValid || DiagramCanvas.DesiredSize.Width <= 0 || DiagramCanvas.DesiredSize.Height <= 0)
                    return;
                _isFitting = true;
                try { ZoomBox.ZoomUniform(); }
                finally { _isFitting = false; }
            }));
        }

        private void CieDiagramView_Unloaded(object sender, RoutedEventArgs e)
        {
            _pendingFit?.Abort();
            _pendingFit = null;
        }

        private void CieDiagramView_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshTheme();
            EnsureOverlayVisual();
            QueueFit();
            RenderOverlay();
        }

        private void DiagramCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            QueueFit();
            RenderOverlay();
        }

        private void DiagramCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            _cursorXy = CieChromaticity.Empty;
            CursorTextChanged?.Invoke(this, string.Empty);
        }

        private void DiagramCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            CursorTextChanged?.Invoke(this, GetCursorText(e.GetPosition(DiagramCanvas)));
        }

        private void ZoomBox_ContentMatrixChanged(object? sender, EventArgs e)
        {
            if (!_isFitting) _autoFit = false;
            RenderOverlay();
        }

        private void EnsureOverlayVisual()
        {
            if (!DiagramCanvas.ContainsVisual(_segmentVisual)) DiagramCanvas.AddVisual(_segmentVisual);
            if (!DiagramCanvas.ContainsVisual(_overlayVisual))
            {
                DiagramCanvas.AddVisual(_overlayVisual);
            }
        }

        private void RenderOverlay()
        {
            EnsureOverlayVisual();
            using (DrawingContext dc = _segmentVisual.RenderOpen())
            {
                if (_background != null)
                {
                    Point Transform(CieChromaticity xy)
                    {
                        Point p = _profile.ToImagePixel(xy);
                        return new(p.X * DiagramCanvas.ActualWidth / _background.PixelWidth, p.Y * DiagramCanvas.ActualHeight / _background.PixelHeight);
                    }
                    foreach (CieDiagramSegment segment in _segments)
                    {
                        Point a = Transform(segment.Start), b = Transform(segment.End);
                        if (!double.IsFinite(a.X) || !double.IsFinite(a.Y) || !double.IsFinite(b.X) || !double.IsFinite(b.Y)) continue;
                        Pen pen = new(new SolidColorBrush(segment.Color), 1.8 * GetLayoutScale());
                        if (segment.Dashed) pen.DashStyle = DashStyles.Dash;
                        dc.DrawLine(pen, a, b);
                    }
                }
            }

            Size canvasSize = new(DiagramCanvas.ActualWidth, DiagramCanvas.ActualHeight);
            Size bitmapPixelSize = _background == null
                ? Size.Empty
                : new Size(_background.PixelWidth, _background.PixelHeight);

            _overlayVisual.Render(
                _profile,
                canvasSize,
                bitmapPixelSize,
                GetLayoutScale(),
                _gamuts,
                _referenceMarkers.Concat(_markers).ToList(),
                _showCctReference,
                _showDaylightReference,
                _selectedMarker);
        }

        private double GetLayoutScale()
        {
            double zoom = ZoomBox.ContentMatrix.M11;
            return double.IsNaN(zoom) || double.IsInfinity(zoom) || zoom <= 0 ? 1 : 1 / zoom;
        }

        internal string GetCursorText(Point canvasPoint)
        {
            _cursorXy = GetChromaticityAt(canvasPoint);
            return _cursorXy.IsFinite ? new CiePointReadout(_cursorXy, ReferenceWhite).CursorText : string.Empty;
        }

        private void RefreshTheme()
        {
            if (DiagramCanvas == null) return;
            Color color = (GetValue(ThemeBackgroundProperty) as SolidColorBrush)?.Color ?? Colors.White;
            bool dark = 0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B < 128;
            Background = dark ? Brushes.Black : Brushes.White;
            if (_background != null && _dark == dark) return;
            _dark = dark;
            _background = LoadBackground(_profile, dark);
            DiagramCanvas.Source = _background;
            RenderOverlay();
        }

        private static BitmapSource LoadBackground(CieDiagramProfile profile, bool dark)
        {
            if (string.IsNullOrWhiteSpace(profile.BackgroundUri))
            {
                return CieBackgroundCache.Get(profile, dark);
            }

            return LoadBitmap(profile.BackgroundUri);
        }

        private static BitmapImage LoadBitmap(string uri)
        {
            BitmapImage image = new();
            image.BeginInit();
            image.UriSource = new Uri(uri, UriKind.RelativeOrAbsolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}
