using ColorVision.ImageEditor.EditorTools.ThreeD;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.ImageEditor
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Window Closed cancels outstanding work and disposes the renderer.")]
    public partial class Window3D : Window
    {
        private readonly WriteableBitmap colorBitmap;
        private readonly double? initialHeightScaleOverride;
        private HeightMapDxRenderer? renderer;
        private readonly HeightMapOrbitCamera orbit = new();
        private CancellationTokenSource? buildCancellation;
        private readonly CancellationTokenSource lifetimeCancellation = new();
        private bool exporting;
        private ColormapInfo? currentColormap;
        private double heightScale = 100;
        private bool isClosed, loaded, cameraMoving, fitted = true, topView;
        private MouseButton? dragButton;
        private Point lastMousePosition;
        private long lastFrame, lastInteraction, lastHover;
        private bool usingInteractionMesh;
        internal bool IsReady { get; private set; }
        internal double SamplingMilliseconds { get; private set; }
        internal object? BuildMetrics { get; private set; }

        private static readonly string[] ColormapNames =
        {
            "jet", "viridis", "plasma", "inferno", "magma", "cividis", "turbo", "hot", "cool", "spring", "summer", "autumn", "winter",
            "bone", "pink", "ocean", "rainbow", "hsv", "deepgreen", "parula", "twilight", "twilight_shifted", "mkpj1", "mkpj2"
        };
        private static readonly Lazy<List<ColormapInfo>> AllColormaps = new(() =>
        {
            var list = new List<ColormapInfo>();
            foreach (var name in ColormapNames)
            {
                try
                {
                    var image = new BitmapImage(new Uri($"pack://application:,,,/ColorVision.ImageEditor;component/Assets/Colormap/colorscale_{name}.jpg"));
                    image.Freeze();
                    list.Add(new ColormapInfo(name, image, ExtractLutFromColormap(image)));
                }
                catch (IOException) { }
            }
            return list;
        });

        private static byte[] ExtractLutFromColormap(BitmapSource image)
        {
            var bgr = new FormatConvertedBitmap(image, PixelFormats.Bgr24, null, 0);
            int w = bgr.PixelWidth, h = bgr.PixelHeight, stride = w * 3;
            byte[] pixels = new byte[h * stride], lut = new byte[256 * 3];
            bgr.CopyPixels(pixels, stride, 0);
            for (int i = 0; i < 256; i++)
            {
                int row = Math.Clamp((int)((1.0 - i / 255.0) * (h - 1)), 0, h - 1);
                Buffer.BlockCopy(pixels, row * stride + w / 2 * 3, lut, i * 3, 3);
            }
            return lut;
        }

        public static Window3DConfig Config => Window3DConfig.Instance;
        public Window3D(WriteableBitmap writeableBitmap, double? initialHeightScaleOverride = null)
        {
            ArgumentNullException.ThrowIfNull(writeableBitmap);
            colorBitmap = writeableBitmap;
            this.initialHeightScaleOverride = initialHeightScaleOverride;
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (loaded) return;
            loaded = true;
            heightScale = ValidHeight(initialHeightScaleOverride is > 0 ? initialHeightScaleOverride.Value : Config.DefaultHeightScale);
            HeightValue.Text = heightScale.ToString("F1");
            SourceInfo.Text = $"源图 {colorBitmap.PixelWidth:N0} × {colorBitmap.PixelHeight:N0} · 显示亮度 0–255";
            try
            {
                renderer = new HeightMapDxRenderer();
                renderer.Camera.FieldOfView = HeightMapOrbitCamera.FieldOfView;
                ContentGrid.Children.Add(renderer.Viewport);
                renderer.Viewport.PreviewMouseDown += Viewport_MouseDown;
                renderer.Viewport.PreviewMouseMove += Viewport_MouseMove;
                renderer.Viewport.PreviewMouseUp += Viewport_MouseUp;
                renderer.Viewport.PreviewMouseWheel += Viewport_MouseWheel;
                renderer.Viewport.LostMouseCapture += Viewport_LostMouseCapture;
                renderer.Viewport.RenderExceptionOccurred += Viewport_RenderExceptionOccurred;
                CompositionTarget.Rendering += OnRendering;
                var maps = AllColormaps.Value;
                ComboBoxColormap.ItemsSource = maps;
                ComboBoxColormap.SelectedIndex = Math.Max(0, maps.FindIndex(c => c.Name == Config.SelectedColormap));
                ApplyLighting();
                await ReloadAsync();
            }
            catch (Exception ex) { ShowFailure(ex); }
        }

        private async Task ReloadAsync()
        {
            if (renderer == null || isClosed) return;
            buildCancellation?.Cancel();
            buildCancellation?.Dispose();
            var cancellation = buildCancellation = new CancellationTokenSource();
            CancellationToken token = cancellation.Token;
            IsReady = false;
            LoadingText.Text = "正在准备高度图…";
            LoadingPanel.Visibility = Visibility.Visible;
            try
            {
                // Source belongs to the editor UI thread. Copy only the sampled rows, not a
                // full frozen clone (a 150 MP source would add hundreds of MB).
                await Dispatcher.Yield(DispatcherPriority.Background);
                token.ThrowIfCancellationRequested();
                var timer = Stopwatch.StartNew();
                int detail = Math.Clamp(Config.DetailResolution, 128, 2048);
                var sample = HeightMapPixelSampler.Sample(colorBitmap, detail, detail, token);
                SamplingMilliseconds = timer.Elapsed.TotalMilliseconds;
                var world = HeightMapPixelSampler.CalculateFitSize(colorBitmap.PixelWidth, colorBitmap.PixelHeight, 512, 512);
                renderer.InteractionMaxWidth = Math.Clamp(Config.TargetPixelsX, 128, 1024);
                renderer.InteractionMaxHeight = Math.Clamp(Config.TargetPixelsY, 128, 1024);
                BuildMetrics = await renderer.LoadAsync(sample, world.Width - 1, world.Height - 1, token);
                if (isClosed || token.IsCancellationRequested) return;
                renderer.SetHeightScale(heightScale);
                renderer.SetColormap(currentColormap?.Lut);
                IsReady = true;
                renderer.SetInteracting(false);
                usingInteractionMesh = false;
                LoadingPanel.Visibility = Visibility.Collapsed;
                if (renderer.Bounds.IsEmpty)
                {
                    LoadingText.Text = "没有可显示的有效曲面（透明像素不会生成网格）。";
                    LoadingPanel.Visibility = Visibility.Visible;
                }
                ResetView(true);
                UpdateStatus();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!isClosed && ReferenceEquals(buildCancellation, cancellation)) ShowFailure(ex); }
        }

        private void ShowFailure(Exception ex)
        {
            IsReady = false;
            LoadingText.Text = $"高度图未能完成：{ex.Message}";
            LoadingPanel.Visibility = Visibility.Visible;
            StatusText.Text = "可在设置中降低细节后重试；需要可用的 DirectX 11 渲染设备。";
        }

        private void Viewport_RenderExceptionOccurred(object? sender, HelixToolkit.SharpDX.Utilities.RelayExceptionEventArgs e)
        {
            if (isClosed) return;
            Dispatcher.BeginInvoke(() => { if (!isClosed) { buildCancellation?.Cancel(); ShowFailure(e.Exception); } });
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            isClosed = true;
            IsReady = false;
            buildCancellation?.Cancel();
            buildCancellation?.Dispose();
            lifetimeCancellation.Cancel();
            lifetimeCancellation.Dispose();
            CompositionTarget.Rendering -= OnRendering;
            SettingsPopup.IsOpen = false;
            if (renderer != null)
            {
                renderer.Viewport.PreviewMouseDown -= Viewport_MouseDown;
                renderer.Viewport.PreviewMouseMove -= Viewport_MouseMove;
                renderer.Viewport.PreviewMouseUp -= Viewport_MouseUp;
                renderer.Viewport.PreviewMouseWheel -= Viewport_MouseWheel;
                renderer.Viewport.LostMouseCapture -= Viewport_LostMouseCapture;
                renderer.Viewport.RenderExceptionOccurred -= Viewport_RenderExceptionOccurred;
                renderer.Viewport.ReleaseMouseCapture();
                ContentGrid.Children.Clear();
                renderer.Dispose();
                renderer = null;
            }
        }

        private void BeginCameraChange(bool keepFit = false)
        {
            if (!IsReady) return;
            if (!keepFit) fitted = false;
            long now = Stopwatch.GetTimestamp();
            if (!cameraMoving) lastFrame = now;
            cameraMoving = true;
            lastInteraction = now;
            SetInteractionMesh(Config.AdaptiveDetail);
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (!IsReady || renderer == null) return;
            long now = Stopwatch.GetTimestamp();
            if (cameraMoving)
            {
                cameraMoving = orbit.Update(Stopwatch.GetElapsedTime(lastFrame, now).TotalSeconds);
                lastFrame = now;
                ApplyCamera();
                lastInteraction = now;
            }
            if (!cameraMoving && dragButton == null && usingInteractionMesh && Stopwatch.GetElapsedTime(lastInteraction, now).TotalMilliseconds >= 160)
                SetInteractionMesh(false);
        }

        private void SetInteractionMesh(bool value)
        {
            if (usingInteractionMesh == value || renderer == null) return;
            usingInteractionMesh = value;
            renderer.SetInteracting(value);
            UpdateStatus();
        }

        private void ApplyCamera()
        {
            if (renderer == null) return;
            renderer.Camera.Position = orbit.Position;
            renderer.Camera.LookDirection = orbit.LookDirection;
            renderer.Camera.UpDirection = orbit.UpDirection;
            renderer.Camera.NearPlaneDistance = Math.Max(0.01, orbit.Distance / 10000);
            renderer.Camera.FarPlaneDistance = Math.Max(10000, orbit.Distance + heightScale + 2000);
        }

        private void ResetView(bool immediate = false)
        {
            if (!IsReady || renderer == null) return;
            fitted = true;
            var bounds = renderer.Bounds.IsEmpty ? new System.Windows.Media.Media3D.Rect3D(0, 0, 0, renderer.WorldWidth, renderer.WorldHeight, 0) : renderer.Bounds;
            orbit.Fit(bounds, ViewArea.ActualWidth / Math.Max(1, ViewArea.ActualHeight), topView, immediate);
            if (immediate) { cameraMoving = false; ApplyCamera(); }
            else BeginCameraChange(true);
        }

        private void ViewArea_SizeChanged(object sender, SizeChangedEventArgs e) { if (fitted) ResetView(true); }
        private void ResetViewButton_Click(object sender, RoutedEventArgs e) { topView = false; ResetView(); }
        private void TopView_Click(object sender, RoutedEventArgs e) { topView = true; ResetView(); }

        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsReady || renderer == null) return;
            if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left) { topView = false; ResetView(); e.Handled = true; return; }
            if (e.ChangedButton is not (MouseButton.Left or MouseButton.Right or MouseButton.Middle)) return;
            dragButton = e.ChangedButton;
            lastMousePosition = e.GetPosition(renderer.Viewport);
            renderer.Viewport.Focus();
            renderer.Viewport.CaptureMouse();
            SetInteractionMesh(Config.AdaptiveDetail);
            e.Handled = true;
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (!IsReady || renderer == null) return;
            Point pos = e.GetPosition(renderer.Viewport);
            if (dragButton != null)
            {
                Vector delta = pos - lastMousePosition;
                lastMousePosition = pos;
                if (dragButton == MouseButton.Left && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) orbit.Orbit(delta.X * 0.3, delta.Y * 0.3);
                else orbit.Pan(delta.X, delta.Y, ViewArea.ActualHeight);
                BeginCameraChange();
                e.Handled = true;
            }
            else if (!cameraMoving && Stopwatch.GetElapsedTime(lastHover).TotalMilliseconds > 60)
            {
                lastHover = Stopwatch.GetTimestamp();
                if (renderer.TryHit(pos, out var hit))
                {
                    double sourceX = hit.Position.X / Math.Max(renderer.WorldWidth, 1) * (colorBitmap.PixelWidth - 1);
                    double sourceY = (1 - hit.Position.Y / Math.Max(renderer.WorldHeight, 1)) * (colorBitmap.PixelHeight - 1);
                    HoverInfoText.Text = $"源图坐标 X {sourceX:F1}  Y {sourceY:F1}    采样灰度 {hit.Gray} / 255    Z(灰度) {hit.Gray / 255.0 * heightScale:F2}    曲面交点 Z {hit.Position.Z:F2}";
                }
                else HoverInfoText.Text = "将鼠标移到曲面上读取显示亮度（0–255）；透明区域没有曲面。";
            }
        }

        private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (dragButton != e.ChangedButton) return;
            dragButton = null;
            lastInteraction = Stopwatch.GetTimestamp();
            renderer?.Viewport.ReleaseMouseCapture();
            e.Handled = true;
        }
        private void Viewport_LostMouseCapture(object sender, MouseEventArgs e) { dragButton = null; lastInteraction = Stopwatch.GetTimestamp(); }
        private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e) { if (!IsReady) return; orbit.Zoom(e.Delta / 120.0); BeginCameraChange(); e.Handled = true; }
        private void Pan(double x, double y) { orbit.Pan(x, y, ViewArea.ActualHeight); BeginCameraChange(); }
        private void Rotate(double x, double y) { orbit.Orbit(x, y); BeginCameraChange(); }
        private void CameraMoveLeft_Click(object sender, RoutedEventArgs e) => Pan(30, 0);
        private void CameraMoveRight_Click(object sender, RoutedEventArgs e) => Pan(-30, 0);
        private void CameraMoveForward_Click(object sender, RoutedEventArgs e) => Pan(0, 30);
        private void CameraMoveBack_Click(object sender, RoutedEventArgs e) => Pan(0, -30);
        private void LookLeft_Click(object sender, RoutedEventArgs e) => Rotate(-12, 0);
        private void LookRight_Click(object sender, RoutedEventArgs e) => Rotate(12, 0);
        private void LookUp_Click(object sender, RoutedEventArgs e) => Rotate(0, 10);
        private void LookDown_Click(object sender, RoutedEventArgs e) => Rotate(0, -10);
        private static double ValidHeight(double value) => double.IsFinite(value) ? Math.Clamp(value, 0.1, 10000) : 100;
        private void SetHeight(double value)
        {
            heightScale = ValidHeight(value);
            HeightValue.Text = heightScale.ToString("F1");
            renderer?.SetHeightScale(heightScale);
            if (fitted) ResetView();
            UpdateStatus();
        }
        private void HeightScaleIncrease_Click(object sender, RoutedEventArgs e) => SetHeight(heightScale * 1.1);
        private void HeightScaleDecrease_Click(object sender, RoutedEventArgs e) => SetHeight(heightScale * 0.9);
        private void HeightValue_LostFocus(object sender, RoutedEventArgs e) { if (double.TryParse(HeightValue.Text, out double value)) SetHeight(value); else HeightValue.Text = heightScale.ToString("F1"); }
        private void HeightValue_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { HeightValue_LostFocus(sender, e); e.Handled = true; } }
        private void Window3D_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.OriginalSource is TextBox || Keyboard.FocusedElement is TextBox
                || ComboBoxColormap.IsKeyboardFocusWithin || LightingSlider.IsKeyboardFocusWithin || SettingsPopup.IsOpen) return;
            switch (e.Key)
            {
                case Key.Add: case Key.OemPlus: SetHeight(heightScale * 1.1); break;
                case Key.Subtract: case Key.OemMinus: SetHeight(heightScale * 0.9); break;
                case Key.Home: topView = false; ResetView(); break;
                case Key.Left: case Key.L: Pan(30, 0); break;
                case Key.Right: case Key.R: Pan(-30, 0); break;
                case Key.Up: case Key.T: Pan(0, 30); break;
                case Key.Down: case Key.B: Pan(0, -30); break;
                case Key.A: Rotate(0, 10); break;
                case Key.C: Rotate(0, -10); break;
                case Key.D: Rotate(-12, 0); break;
                case Key.F: Rotate(12, 0); break;
                default: return;
            }
            e.Handled = true;
        }

        private void ComboBoxColormap_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxColormap.SelectedItem is not ColormapInfo info) return;
            currentColormap = info;
            Config.SelectedColormap = info.Name;
            ColorBarImage.Source = info.ImageSource;
            renderer?.SetColormap(info.Lut);
        }
        private void ApplyLighting() { if (renderer != null) renderer.SetLighting(1 - LightingSlider.Value * 0.55, LightingSlider.Value); }
        private void LightingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyLighting();
        private void UpdateStatus()
        {
            if (renderer == null || !IsReady) return;
            StatusText.Text = $"{(usingInteractionMesh ? "交互细节" : "完整细节")} · 采样 {renderer.ActiveSample.Width} × {renderer.ActiveSample.Height} · 显示高度 {heightScale:F1} · 双线性亮度采样，未做深度重建";
        }
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            TxtDetail.Text = Config.DetailResolution.ToString();
            TxtTargetX.Text = Config.TargetPixelsX.ToString();
            TxtTargetY.Text = Config.TargetPixelsY.ToString();
            AdaptiveCheck.IsChecked = Config.AdaptiveDetail;
            SettingsError.Text = "";
            SettingsPopup.IsOpen = true;
        }
        private async void ApplySettings_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtDetail.Text, out int detail) || detail < 128 || detail > 2048
                || !int.TryParse(TxtTargetX.Text, out int x) || x < 128 || x > 1024
                || !int.TryParse(TxtTargetY.Text, out int y) || y < 128 || y > 1024)
            {
                SettingsError.Text = "请输入上述范围内的整数。";
                return;
            }
            Config.DetailResolution = detail;
            Config.TargetPixelsX = x;
            Config.TargetPixelsY = y;
            Config.AdaptiveDetail = AdaptiveCheck.IsChecked == true;
            SettingsPopup.IsOpen = false;
            await ReloadAsync();
        }
        private void ScreenshotButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsReady || renderer == null) return;
            var dialog = new SaveFileDialog { Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap Image|*.bmp", DefaultExt = "png", FileName = $"3DView_{DateTime.Now:yyyyMMdd_HHmmss}.png" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                SetInteractionMesh(false);
                var bitmap = renderer.CaptureBitmap((int)ViewArea.ActualWidth, (int)ViewArea.ActualHeight)
                    ?? throw new InvalidOperationException("渲染设备未能提供截图。");
                BitmapEncoder encoder = Path.GetExtension(dialog.FileName).ToLowerInvariant() switch { ".jpg" => new JpegBitmapEncoder(), ".bmp" => new BmpBitmapEncoder(), _ => new PngBitmapEncoder() };
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(dialog.FileName);
                encoder.Save(stream);
                StatusText.Text = $"截图已保存：{dialog.FileName}";
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "截图失败", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private async void ExportModelButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsReady || renderer == null || exporting || renderer.Bounds.IsEmpty) return;
            var dialog = new SaveFileDialog { Filter = "OBJ Model|*.obj|STL Model|*.stl", DefaultExt = "obj", FileName = $"3DView_{DateTime.Now:yyyyMMdd_HHmmss}.obj" };
            if (dialog.ShowDialog(this) != true) return;
            exporting = true;
            StatusText.Text = "正在导出完整细节模型…";
            try
            {
                // Capture the current immutable grid, LUT and display scale. A later reload or
                // camera movement cannot change the model being written in the background.
                await HeightMapModelExporter.ExportAsync(renderer.ExportGeometry, renderer.ExportLut, heightScale, dialog.FileName,
                    ModelViewer3DConfig.Instance.HideExportedTextureFiles, lifetimeCancellation.Token);
                if (!isClosed) StatusText.Text = $"模型已导出：{dialog.FileName}";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!isClosed) MessageBox.Show(this, ex.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { exporting = false; }
        }
    }
}
