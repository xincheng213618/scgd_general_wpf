using Conoscope.Core;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Conoscope.Presentation
{
    public partial class ConoscopeAngularAnalysisWindow : Window
    {
        private readonly double maximum;
        private bool initialized;
        private bool updatingRegion;
        private Point? dragStart;
        internal ConoscopeAngularAnalysisOptions Options { get; private set; } = new();

        internal ConoscopeAngularAnalysisWindow(ConoscopeExportContext source)
        {
            new ConoscopeAngularAnalysisOptions().Validate(source.MaxAngle);
            maximum = source.MaxAngle;
            InitializeComponent();
            ColorVision.Themes.ThemeManagerExtensions.ApplyCaption(this);
            // The dialog retains only a small frozen preview, never the source callbacks or full XYZ buffers.
            SourcePreview.Source = CreatePreview(source);
            EmitterSize.Text = 0.5.ToString(CultureInfo.CurrentCulture);
            PreviewAxis.Text = CompositeFormatCache.Format(Properties.Resources.AngularPreviewAxes, maximum);
            SetRegion(ConoscopeAngularAnalysisOptions.DefaultRegion(SelectedDirection, maximum));
            initialized = true;
            RefreshOptions();
        }

        private ConoscopeMirrorDirection SelectedDirection => (ConoscopeMirrorDirection)(MirrorDirection.SelectedIndex + 1);
        private void Settings_Changed(object sender, RoutedEventArgs e) { if (initialized && !updatingRegion) RefreshOptions(); }
        private void Direction_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!initialized) return;
            SetRegion(ConoscopeAngularAnalysisOptions.DefaultRegion(SelectedDirection, maximum));
            RefreshOptions();
        }
        private void ResetRegion_Click(object sender, RoutedEventArgs e)
        {
            SetRegion(ConoscopeAngularAnalysisOptions.DefaultRegion(SelectedDirection, maximum));
            RefreshOptions();
        }

        internal bool TryGetOptions(out ConoscopeAngularAnalysisOptions options, out string error)
        {
            options = new();
            error = string.Empty;
            bool intensity = IntensityOption.IsChecked == true, mirror = MirrorEnabled.IsChecked == true;
            double size = 0.5;
            if (intensity && (!TryNumber(EmitterSize.Text, out size) || size <= 0))
            { error = Properties.Resources.AngularInvalidSize; return false; }
            ConoscopeAngularRegion? region = null;
            if (mirror)
            {
                if (!TryNumber(MinX.Text, out double minX) || !TryNumber(MaxX.Text, out double maxX)
                    || !TryNumber(MinY.Text, out double minY) || !TryNumber(MaxY.Text, out double maxY))
                { error = Properties.Resources.AngularInvalidRegion; return false; }
                region = new(minX, maxX, minY, maxY);
            }
            options = new()
            {
                Quantity = intensity ? ConoscopeAngularQuantity.LuminousIntensity : ConoscopeAngularQuantity.Luminance,
                SizeMode = SizeMode.SelectedIndex == 0 ? ConoscopeEmitterSizeMode.CircularDiameter : ConoscopeEmitterSizeMode.Area,
                DiameterMillimeters = SizeMode.SelectedIndex == 0 ? size : 0.5,
                AreaSquareMillimeters = SizeMode.SelectedIndex == 1 ? size : 1,
                MirrorDirection = mirror ? SelectedDirection : ConoscopeMirrorDirection.None,
                MirrorRegion = region
            };
            if (intensity && (!double.IsFinite(options.AreaSquareMeters) || options.AreaSquareMeters <= 0))
            { error = Properties.Resources.AngularInvalidSize; return false; }
            try { options.Validate(maximum); return true; }
            catch (ArgumentException) { error = Properties.Resources.AngularInvalidRegion; return false; }
        }

        private void RefreshOptions()
        {
            EmitterPanel.IsEnabled = IntensityOption.IsChecked == true;
            MirrorPanel.IsEnabled = MirrorEnabled.IsChecked == true;
            EmitterUnit.Text = SizeMode.SelectedIndex == 0 ? "mm" : "mm²";
            bool valid = TryGetOptions(out var options, out string error);
            GenerateButton.IsEnabled = valid;
            ValidationError.Text = error;
            bool showRegion = valid && options.MirrorDirection != ConoscopeMirrorDirection.None;
            TargetRectangle.Visibility = ReferenceRectangle.Visibility = showRegion ? Visibility.Visible : Visibility.Collapsed;
            PreviewCanvas.Cursor = MirrorEnabled.IsChecked == true ? Cursors.Cross : Cursors.Arrow;
            if (!showRegion) return;
            var target = options.GetMirrorRegion(maximum);
            DrawRectangle(TargetRectangle, target);
            var reference = options.MirrorDirection is ConoscopeMirrorDirection.TopToBottom or ConoscopeMirrorDirection.BottomToTop
                ? new ConoscopeAngularRegion(target.MinX, target.MaxX, -target.MaxY, -target.MinY)
                : new ConoscopeAngularRegion(-target.MaxX, -target.MinX, target.MinY, target.MaxY);
            DrawRectangle(ReferenceRectangle, reference);
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetOptions(out var options, out string error)) { ValidationError.Text = error; return; }
            Options = options;
            DialogResult = true;
        }

        private static bool TryNumber(string text, out double value) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && double.IsFinite(value);

        private void SetRegion(ConoscopeAngularRegion region)
        {
            updatingRegion = true;
            try
            {
                MinX.Text = region.MinX.ToString("0.##", CultureInfo.CurrentCulture);
                MaxX.Text = region.MaxX.ToString("0.##", CultureInfo.CurrentCulture);
                MinY.Text = region.MinY.ToString("0.##", CultureInfo.CurrentCulture);
                MaxY.Text = region.MaxY.ToString("0.##", CultureInfo.CurrentCulture);
            }
            finally { updatingRegion = false; }
        }

        private void DrawRectangle(Rectangle rectangle, ConoscopeAngularRegion region)
        {
            double scale = PreviewCanvas.Width / (2 * maximum);
            Canvas.SetLeft(rectangle, (region.MinX + maximum) * scale);
            Canvas.SetTop(rectangle, (maximum - region.MaxY) * scale);
            rectangle.Width = (region.MaxX - region.MinX) * scale;
            rectangle.Height = (region.MaxY - region.MinY) * scale;
        }

        private Point GetAngularPoint(MouseEventArgs e)
        {
            Point point = e.GetPosition(PreviewCanvas);
            double scale = 2 * maximum / PreviewCanvas.Width;
            return new(point.X * scale - maximum, maximum - point.Y * scale);
        }
        private void Preview_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (MirrorEnabled.IsChecked != true) return;
            Point point = GetAngularPoint(e);
            if (!ConoscopeAngularAnalysisOptions.DefaultRegion(SelectedDirection, maximum).Contains(point.X, point.Y)) return;
            dragStart = point;
            PreviewCanvas.CaptureMouse();
            e.Handled = true;
        }
        private void Preview_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragStart is not { } start || e.LeftButton != MouseButtonState.Pressed) return;
            SelectRegion(start, GetAngularPoint(e));
        }

        internal void SelectRegion(Point start, Point end)
        {
            var bounds = ConoscopeAngularAnalysisOptions.DefaultRegion(SelectedDirection, maximum);
            start.X = Math.Clamp(start.X, bounds.MinX, bounds.MaxX);
            start.Y = Math.Clamp(start.Y, bounds.MinY, bounds.MaxY);
            end.X = Math.Clamp(end.X, bounds.MinX, bounds.MaxX);
            end.Y = Math.Clamp(end.Y, bounds.MinY, bounds.MaxY);
            SetRegion(new(Math.Min(start.X, end.X), Math.Max(start.X, end.X), Math.Min(start.Y, end.Y), Math.Max(start.Y, end.Y)));
            RefreshOptions();
        }
        private void Preview_MouseUp(object sender, MouseButtonEventArgs e)
        {
            dragStart = null;
            PreviewCanvas.ReleaseMouseCapture();
        }
        private void Preview_LostCapture(object sender, MouseEventArgs e) => dragStart = null;

        private static BitmapSource CreatePreview(ConoscopeExportContext source)
        {
            const int size = 256;
            double[] values = new double[size * size];
            double minimum = double.PositiveInfinity, maximum = double.NegativeInfinity;
            for (int row = 0; row < size; row++)
                for (int col = 0; col < size; col++)
                {
                    double x = ((col + 0.5) * 2 / size - 1) * source.MaxAngle;
                    double y = (1 - (row + 0.5) * 2 / size) * source.MaxAngle;
                    int imageX = (int)Math.Round(source.Center.X + x * source.PixelsPerDegree);
                    int imageY = (int)Math.Round(source.Center.Y - y * source.PixelsPerDegree);
                    double value = x * x + y * y > source.MaxAngle * source.MaxAngle || imageX < 0 || imageY < 0
                        || imageX >= source.ImageWidth || imageY >= source.ImageHeight ? double.NaN : source.ReadXyz(imageX, imageY).Y;
                    values[row * size + col] = value;
                    if (double.IsFinite(value)) { minimum = Math.Min(minimum, value); maximum = Math.Max(maximum, value); }
                }
            byte[] pixels = new byte[size * size * 4];
            for (int i = 0; i < values.Length; i++)
            {
                if (!double.IsFinite(values[i])) continue;
                byte gray = maximum > minimum ? (byte)(Math.Clamp((values[i] - minimum) / (maximum - minimum), 0, 1) * 255) : (byte)128;
                pixels[i * 4] = pixels[i * 4 + 1] = pixels[i * 4 + 2] = gray;
                pixels[i * 4 + 3] = 255;
            }
            var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
