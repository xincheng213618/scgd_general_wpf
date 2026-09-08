#pragma warning disable CA1001
using ColorVision.Engine.Media;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.POI;
using ColorVision.Engine.Templates;
using ColorVision.Themes;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace ColorVision.Engine.Services.PhyCameras.Calibration
{
    public partial class LumFourColorCalibrationWorkflowWindow : Window
    {
        private readonly LumFourColorCalibrationSession session = new();
        private CancellationTokenSource? captureCancellation;
        private CVRawManualCieConfig? correctedConfig;
        private Rectangle? poiVisual;
        private Point drawStart;
        private bool drawMode;
        private bool pointerDrawing;
        private bool busy;

        private LumFourColorCalibrationSample? SelectedSample => SampleList.SelectedItem as LumFourColorCalibrationSample;

        public LumFourColorCalibrationWorkflowWindow(string? sourcePath = null)
            : this(
                ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList(),
                ServiceManager.GetInstance().DeviceServices.OfType<DeviceSpectrum>().ToList(),
                sourcePath)
        {
        }

        internal LumFourColorCalibrationWorkflowWindow(
            IEnumerable<DeviceCamera> cameras,
            IEnumerable<DeviceSpectrum> spectrums,
            string? sourcePath = null)
        {
            InitializeComponent();
            this.ApplyCaption();
            SourcePathBox.Text = sourcePath ?? string.Empty;
            SampleList.ItemsSource = session.Samples;
            CameraCombo.ItemsSource = cameras.ToList();
            SpectrumCombo.ItemsSource = spectrums.ToList();
            CameraCombo.SelectedIndex = CameraCombo.Items.Count > 0 ? 0 : -1;
            SpectrumCombo.SelectedIndex = SpectrumCombo.Items.Count > 0 ? 0 : -1;
            SetMode(false);
        }

        public static void ShowWindow(string? sourcePath = null)
        {
            LumFourColorCalibrationWorkflowWindow? existing = Application.Current.Windows
                .OfType<LumFourColorCalibrationWorkflowWindow>()
                .FirstOrDefault();
            if (existing != null)
            {
                if (!string.IsNullOrWhiteSpace(sourcePath))
                    existing.SourcePathBox.Text = sourcePath;
                if (existing.WindowState == WindowState.Minimized)
                    existing.WindowState = WindowState.Normal;
                existing.Activate();
                return;
            }

            Window? owner = Application.Current.GetActiveWindow();
            LumFourColorCalibrationWorkflowWindow window = new(sourcePath)
            {
                Owner = owner,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            };
            window.Show();
        }

        private void BrowseSource_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Title = "选择原四色校正文件",
                Filter = "四色校正文件 (*.dat;*.json)|*.dat;*.json|所有文件 (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false,
            };
            if (dialog.ShowDialog(this) == true)
                SourcePathBox.Text = dialog.FileName;
        }

        private void SourcePathBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            correctedConfig = null;
            if (SaveButton != null)
                SaveButton.IsEnabled = false;
            RefreshActions();
        }

        private void CorrectionMode_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized || SampleList == null)
                return;
            SetMode(SinglePointMode.IsChecked == true);
        }

        private void SetMode(bool singlePoint)
        {
            session.SetMode(singlePoint);
            correctedConfig = null;
            SaveButton.IsEnabled = false;
            SampleList.SelectedIndex = 0;
            StatusText.Text = singlePoint ? "完成一次相机与光谱采集，顺序不限。" : "按现场顺序完成 R、G、B、W。";
            RefreshSelectedSample();
        }

        private void CameraCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DeviceCamera? camera = CameraCombo.SelectedItem as DeviceCamera;
            CalibrationCombo.ItemsSource = camera?.PhyCamera?.CalibrationParams;
            CalibrationCombo.SelectedIndex = CalibrationCombo.Items.Count > 0 ? 0 : -1;
            RefreshActions();
        }

        private void SampleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CancelDrawMode();
            RefreshSelectedSample();
        }

        private async void CaptureCamera_Click(object sender, RoutedEventArgs e)
        {
            LumFourColorCalibrationSample? sample = SelectedSample;
            if (sample == null || CameraCombo.SelectedItem is not DeviceCamera camera
                || CalibrationCombo.SelectedItem is not TemplateModel<CalibrationParam> calibration)
            {
                ShowError("请选择相机和校正模板。");
                return;
            }

            await RunCaptureAsync($"正在采集 {sample.Name} 的 CIE 图像...", async token =>
            {
                LumFourColorCieCapture frame = await new LocalLumFourColorCameraCaptureProvider(camera, calibration.Value)
                    .CaptureAsync(sample.Target, token);
                sample.SetFrame(frame, LumFourColorCieService.Render(frame));
                InvalidateCalculation();
                StatusText.Text = sample.HasSpectrumMeasurement
                    ? $"{sample.Name} 取图完成，请绘制 POI；光谱数据已保留。"
                    : $"{sample.Name} 取图完成，请绘制 POI。";
                RefreshSelectedSample();
            });
        }

        private void LoadCie_Click(object sender, RoutedEventArgs e)
        {
            LumFourColorCalibrationSample? sample = SelectedSample;
            if (sample == null)
                return;

            OpenFileDialog dialog = new()
            {
                Title = $"加载 {sample.Name} 的 CVCIE 图像",
                Filter = "CVCIE 文件 (*.cvcie)|*.cvcie|所有文件 (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false,
            };
            if (dialog.ShowDialog(this) != true)
                return;

            try
            {
                LumFourColorCieCapture frame = LumFourColorCieService.Load(dialog.FileName);
                sample.SetFrame(frame, LumFourColorCieService.Render(frame));
                InvalidateCalculation();
                StatusText.Text = sample.HasSpectrumMeasurement
                    ? $"已加载 {sample.Name} 的 CIE 图像，请绘制 POI；光谱数据已保留。"
                    : $"已加载 {sample.Name} 的 CIE 图像，请绘制 POI。";
                RefreshSelectedSample();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void DrawPoi_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSample?.Frame == null)
                return;

            drawMode = !drawMode;
            pointerDrawing = false;
            DrawPoiButton.Content = drawMode ? "拖动框选 POI" : "绘制 POI";
            PoiCanvas.Cursor = drawMode ? Cursors.Cross : Cursors.Arrow;
            StatusText.Text = drawMode ? "在 CIE 图像上拖动绘制矩形 POI。" : "已退出 POI 绘制。";
        }

        private async void CaptureSpectrum_Click(object sender, RoutedEventArgs e)
        {
            LumFourColorCalibrationSample? sample = SelectedSample;
            if (sample == null)
                return;
            if (SpectrumCombo.SelectedItem is not DeviceSpectrum spectrum)
            {
                ShowError("请选择光谱仪。");
                return;
            }

            await RunCaptureAsync($"正在采集 {sample.Name} 的光谱...", async token =>
            {
                LumFourColorSpectrumCapture result = await new DeviceLumFourColorSpectrumCaptureProvider(spectrum)
                    .CaptureAsync(sample.Target, token);
                sample.SetSpectrumMeasurement(result);
                InvalidateCalculation();
                StatusText.Text = sample.IsComplete
                    ? "采集完成，可以计算校正。"
                    : sample.HasImage
                        ? $"{sample.Name} 光谱采集完成，请绘制 POI。"
                        : $"{sample.Name} 光谱采集完成，可以继续相机取图。";
                RefreshSelectedSample();
            });
        }

        private async Task RunCaptureAsync(string message, Func<CancellationToken, Task> action)
        {
            captureCancellation?.Dispose();
            captureCancellation = new CancellationTokenSource();
            SetBusy(true, message);
            try
            {
                await action(captureCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "操作已取消。";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private void PoiCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!drawMode || SelectedSample?.Frame == null)
                return;

            Rect bounds = GetImageBounds(SelectedSample.Frame);
            Point position = e.GetPosition(PoiCanvas);
            if (!bounds.Contains(position))
                return;

            drawStart = ClampToBounds(position, bounds);
            pointerDrawing = true;
            PoiCanvas.CaptureMouse();
            ShowPoiVisual(new Rect(drawStart, drawStart));
            e.Handled = true;
        }

        private void PoiCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!pointerDrawing || SelectedSample?.Frame == null)
                return;

            Rect bounds = GetImageBounds(SelectedSample.Frame);
            ShowPoiVisual(CreateRect(drawStart, ClampToBounds(e.GetPosition(PoiCanvas), bounds)));
        }

        private void PoiCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            LumFourColorCalibrationSample? sample = SelectedSample;
            if (!pointerDrawing || sample?.Frame == null)
                return;

            pointerDrawing = false;
            PoiCanvas.ReleaseMouseCapture();
            Rect bounds = GetImageBounds(sample.Frame);
            Rect displayRect = CreateRect(drawStart, ClampToBounds(e.GetPosition(PoiCanvas), bounds));
            if (displayRect.Width < 3 || displayRect.Height < 3)
            {
                StatusText.Text = "POI 区域太小，请重新绘制。";
                return;
            }

            int x = Math.Clamp((int)Math.Round((displayRect.Left - bounds.Left) * sample.Frame.Width / bounds.Width), 0, sample.Frame.Width - 1);
            int y = Math.Clamp((int)Math.Round((displayRect.Top - bounds.Top) * sample.Frame.Height / bounds.Height), 0, sample.Frame.Height - 1);
            int width = Math.Clamp((int)Math.Round(displayRect.Width * sample.Frame.Width / bounds.Width), 1, sample.Frame.Width - x);
            int height = Math.Clamp((int)Math.Round(displayRect.Height * sample.Frame.Height / bounds.Height), 1, sample.Frame.Height - y);
            PoiMeasurementPoint poi = new(x, y, width, height, PoiMeasurementShape.Rect);

            try
            {
                PoiMeasurementResult result = LumFourColorCieService.Measure(sample.Frame, poi);
                sample.SetCameraMeasurement(poi, result);
                InvalidateCalculation();
                StatusText.Text = sample.IsComplete
                    ? $"{sample.Name} 采集完成。"
                    : $"{sample.Name} 的 POI 测量完成，可以采集光谱。";
                CancelDrawMode();
                RefreshSelectedSample();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void Calculate_Click(object sender, RoutedEventArgs e)
        {
            if (!CVRawManualCieCalculator.TryLoadLumFourColorCalibrationDefaults(
                    SourcePathBox.Text.Trim(), out CVRawManualCieConfig source, out string? errorMessage))
            {
                ShowError(errorMessage ?? "无法读取原四色校正文件。");
                return;
            }

            try
            {
                correctedConfig = session.Calculate(source);
                SaveButton.IsEnabled = true;
                StatusText.Text = "校正计算完成，可以保存结果。";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (correctedConfig == null)
                return;

            string sourcePath = SourcePathBox.Text.Trim();
            string sourceDirectory = Path.GetDirectoryName(sourcePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string sourceName = Path.GetFileNameWithoutExtension(sourcePath);
            string extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(extension))
                extension = ".dat";

            SaveFileDialog dialog = new()
            {
                Title = "保存校正后的四色校正文件",
                Filter = "四色校正文件 (*.dat)|*.dat|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                InitialDirectory = Directory.Exists(sourceDirectory) ? sourceDirectory : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                FileName = $"{sourceName}_Corrected{extension}",
                AddExtension = true,
                DefaultExt = extension.TrimStart('.'),
            };
            if (dialog.ShowDialog(this) != true)
                return;

            try
            {
                File.WriteAllText(dialog.FileName, LumFourColorCorrectionCalculator.SerializeCalibrationFile(correctedConfig), new UTF8Encoding(false));
                StatusText.Text = $"已保存：{dialog.FileName}";
            }
            catch (Exception ex)
            {
                ShowError($"保存失败：{ex.Message}");
            }
        }

        private void RefreshSelectedSample()
        {
            LumFourColorCalibrationSample? sample = SelectedSample;
            MeasurementPanel.DataContext = sample;
            CieImage.Source = sample?.Preview;
            CurrentTargetText.Text = sample == null ? string.Empty : $"当前：{sample.Name}";
            EmptyImagePanel.Visibility = sample?.HasImage == true ? Visibility.Collapsed : Visibility.Visible;
            SpectrumGrid.ItemsSource = sample?.Spectrum;
            SpectrumCountText.Text = sample == null ? string.Empty : $"{sample.Spectrum.Count} 点";
            DrawStoredPoi();
            RefreshActions();
        }

        private void RefreshActions()
        {
            if (CaptureCameraButton == null)
                return;

            LumFourColorCalibrationSample? sample = SelectedSample;
            CaptureCameraButton.IsEnabled = !busy && sample != null && CameraCombo.SelectedItem != null && CalibrationCombo.SelectedItem != null;
            LoadCieButton.IsEnabled = !busy && sample != null;
            DrawPoiButton.IsEnabled = !busy && sample?.HasImage == true;
            CaptureSpectrumButton.IsEnabled = !busy && sample != null && SpectrumCombo.SelectedItem != null;
            CalculateButton.IsEnabled = !busy && session.IsComplete && File.Exists(SourcePathBox.Text.Trim());
            SaveButton.IsEnabled = !busy && correctedConfig != null;
        }

        private void SetBusy(bool value, string? message)
        {
            busy = value;
            FourColorMode.IsEnabled = !value;
            SinglePointMode.IsEnabled = !value;
            CameraCombo.IsEnabled = !value;
            CalibrationCombo.IsEnabled = !value;
            SpectrumCombo.IsEnabled = !value;
            SampleList.IsEnabled = !value;
            if (message != null)
                StatusText.Text = message;
            RefreshActions();
        }

        private void DrawStoredPoi()
        {
            PoiCanvas.Children.Clear();
            poiVisual = null;
            LumFourColorCalibrationSample? sample = SelectedSample;
            if (sample?.Frame == null || sample.Poi is not PoiMeasurementPoint poi)
                return;

            Rect bounds = GetImageBounds(sample.Frame);
            Rect displayRect = new(
                bounds.Left + poi.X * bounds.Width / sample.Frame.Width,
                bounds.Top + poi.Y * bounds.Height / sample.Frame.Height,
                poi.Width * bounds.Width / sample.Frame.Width,
                poi.Height * bounds.Height / sample.Frame.Height);
            ShowPoiVisual(displayRect);
        }

        private void ShowPoiVisual(Rect rect)
        {
            if (poiVisual == null)
            {
                poiVisual = new Rectangle
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(Color.FromArgb(28, 0, 122, 204)),
                    IsHitTestVisible = false,
                };
                PoiCanvas.Children.Add(poiVisual);
            }
            Canvas.SetLeft(poiVisual, rect.Left);
            Canvas.SetTop(poiVisual, rect.Top);
            poiVisual.Width = rect.Width;
            poiVisual.Height = rect.Height;
        }

        private Rect GetImageBounds(LumFourColorCieCapture frame)
        {
            double scale = Math.Min(PoiCanvas.ActualWidth / frame.Width, PoiCanvas.ActualHeight / frame.Height);
            double width = frame.Width * scale;
            double height = frame.Height * scale;
            return new Rect((PoiCanvas.ActualWidth - width) / 2, (PoiCanvas.ActualHeight - height) / 2, width, height);
        }

        private static Point ClampToBounds(Point point, Rect bounds)
        {
            return new Point(Math.Clamp(point.X, bounds.Left, bounds.Right), Math.Clamp(point.Y, bounds.Top, bounds.Bottom));
        }

        private static Rect CreateRect(Point first, Point second)
        {
            return new Rect(new Point(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y)),
                new Point(Math.Max(first.X, second.X), Math.Max(first.Y, second.Y)));
        }

        private void CancelDrawMode()
        {
            drawMode = false;
            pointerDrawing = false;
            if (PoiCanvas.IsMouseCaptured)
                PoiCanvas.ReleaseMouseCapture();
            PoiCanvas.Cursor = Cursors.Arrow;
            DrawPoiButton.Content = "绘制 POI";
        }

        private void InvalidateCalculation()
        {
            correctedConfig = null;
            SaveButton.IsEnabled = false;
        }

        private void ImageSurface_SizeChanged(object sender, SizeChangedEventArgs e) => DrawStoredPoi();

        private void OpenManual_Click(object sender, RoutedEventArgs e) => LumFourColorCorrectionWindow.ShowWindow(SourcePathBox.Text.Trim());

        private void ShowError(string message)
        {
            StatusText.Text = message;
            MessageBox.Show(this, message, "四色校正采集", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Closed(object? sender, EventArgs e)
        {
            captureCancellation?.Cancel();
            captureCancellation?.Dispose();
        }
    }
}
