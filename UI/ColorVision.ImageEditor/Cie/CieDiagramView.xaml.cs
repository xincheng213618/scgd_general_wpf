using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.Cie
{
    public partial class CieDiagramView : System.Windows.Controls.UserControl
    {
        private readonly CieOverlayVisual _overlayVisual = new();
        private readonly List<CieGamut> _gamuts = new();
        private readonly List<CieMarker> _markers = new();
        private readonly List<CieMarker> _referenceMarkers = new();
        private CieDiagramProfile _profile = CieDiagramProfiles.Cie1931xy;
        private BitmapSource? _background;
        private CieMarker? _selectedMarker;
        private bool _showCctReference = true;

        public CieDiagramView()
        {
            InitializeComponent();

            Loaded += CieDiagramView_Loaded;
            DiagramCanvas.SizeChanged += DiagramCanvas_SizeChanged;
            DiagramCanvas.MouseLeave += DiagramCanvas_MouseLeave;
            DiagramCanvas.MouseMove += DiagramCanvas_MouseMove;
            ZoomBox.ContentMatrixChanged += ZoomBox_ContentMatrixChanged;

            SetDiagram(CieDiagramKind.Cie1931xy);
        }

        public event EventHandler<string>? CursorTextChanged;

        public CieDiagramKind DiagramKind => _profile.Kind;

        public CieDiagramProfile Profile => _profile;

        public IReadOnlyList<CieGamut> Gamuts => _gamuts;

        public IReadOnlyList<CieMarker> Markers => _markers;

        public IReadOnlyList<CieMarker> ReferenceMarkers => _referenceMarkers;

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

        public void SetDiagram(CieDiagramKind kind)
        {
            _profile = CieDiagramProfiles.Get(kind);
            _background = LoadBackground(_profile);
            DiagramCanvas.Source = _background;
            EnsureOverlayVisual();
            RenderOverlay();

            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                ZoomBox.ZoomUniform();
                RenderOverlay();
            }));
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
            ZoomBox.ZoomUniform();
        }

        private void CieDiagramView_Loaded(object sender, RoutedEventArgs e)
        {
            EnsureOverlayVisual();
            ZoomBox.ZoomUniform();
            RenderOverlay();
        }

        private void DiagramCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderOverlay();
        }

        private void DiagramCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            CursorTextChanged?.Invoke(this, string.Empty);
        }

        private void DiagramCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            CursorTextChanged?.Invoke(this, GetCursorText(e.GetPosition(DiagramCanvas)));
        }

        private void ZoomBox_ContentMatrixChanged(object? sender, EventArgs e)
        {
            RenderOverlay();
        }

        private void EnsureOverlayVisual()
        {
            if (!DiagramCanvas.ContainsVisual(_overlayVisual))
            {
                DiagramCanvas.AddVisual(_overlayVisual);
            }
        }

        private void RenderOverlay()
        {
            EnsureOverlayVisual();

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
                _selectedMarker);
        }

        private double GetLayoutScale()
        {
            double zoom = ZoomBox.ContentMatrix.M11;
            return double.IsNaN(zoom) || double.IsInfinity(zoom) || zoom <= 0 ? 1 : 1 / zoom;
        }

        private string GetCursorText(Point canvasPoint)
        {
            if (_background == null || DiagramCanvas.ActualWidth <= 0 || DiagramCanvas.ActualHeight <= 0)
            {
                return string.Empty;
            }

            Point imagePixel = new(
                canvasPoint.X / DiagramCanvas.ActualWidth * _background.PixelWidth,
                canvasPoint.Y / DiagramCanvas.ActualHeight * _background.PixelHeight);

            CieChromaticity diagramPoint = _profile.ImagePixelToDiagramPoint(imagePixel);
            if (!_profile.ContainsDiagramPoint(diagramPoint))
            {
                return string.Empty;
            }

            CieChromaticity xy = _profile.FromDiagramPoint(diagramPoint);
            if (!xy.IsFinite)
            {
                return string.Empty;
            }

            CieChromaticity uv1960 = CieColorConverter.XyToCie1960uv(xy);
            CieChromaticity uv1976 = CieColorConverter.XyToCie1976uv(xy);

            return string.Create(
                CultureInfo.InvariantCulture,
                $"x={xy.X:F4}  y={xy.Y:F4}    u={uv1960.X:F4}  v={uv1960.Y:F4}    u'={uv1976.X:F4}  v'={uv1976.Y:F4}");
        }

        private static BitmapSource LoadBackground(CieDiagramProfile profile)
        {
            if (string.IsNullOrWhiteSpace(profile.BackgroundUri))
            {
                return CieBackgroundCache.Get(profile);
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
