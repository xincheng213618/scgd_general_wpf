#pragma warning disable CA1001
using ColorVision.Engine.Media;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Themes;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ColorVision.UI;

namespace ColorVision.Engine.Services.PhyCameras.Calibration
{
    public partial class LumFourColorCalibrationWorkflowWindow : Window
    {
        private readonly LumFourColorCalibrationSession session = new();
        private CancellationTokenSource? captureCancellation;
        private CVRawManualCieConfig? correctedConfig;
        private LumFourColorSourceSnapshot? sourceSnapshot;
        private bool closed;
        private LumFourColorPoiEditor? poiEditor;
        private readonly LumFourColorPoiOptions poiOptions;
        private bool sourceFromTemplate;
        private bool busy;

        private LumFourColorCalibrationSample? SelectedSample => SampleList.SelectedItem as LumFourColorCalibrationSample;

        public LumFourColorCalibrationWorkflowWindow(string? sourcePath = null)
            : this(
                ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList(),
                ServiceManager.GetInstance().DeviceServices.OfType<DeviceSpectrum>().ToList(),
                sourcePath, ConfigService.Instance?.GetRequiredService<LumFourColorPoiOptions>())
        {
        }

        internal LumFourColorCalibrationWorkflowWindow(
            IEnumerable<DeviceCamera> cameras,
            IEnumerable<DeviceSpectrum> spectrums,
            string? sourcePath = null, LumFourColorPoiOptions? options = null)
        {
            poiOptions = options ?? new LumFourColorPoiOptions();
            InitializeComponent();
            Width = Math.Min(Width, SystemParameters.WorkArea.Width * 0.96);
            Height = Math.Min(Height, SystemParameters.WorkArea.Height * 0.96);
            poiEditor = new LumFourColorPoiEditor(CieImageView, poiOptions, message => StatusText.Text = message);
            PoiShapeCombo.SelectedIndex = poiOptions.UseRectangle ? 1 : 0;
            this.ApplyCaption();
            SourcePathBox.Text = sourcePath ?? string.Empty;
            SampleList.ItemsSource = session.Samples;
            CameraCombo.ItemsSource = cameras.ToList();
            SpectrumCombo.ItemsSource = spectrums.ToList();
            CameraCombo.SelectedIndex = CameraCombo.Items.Count == 1 ? 0 : -1;
            SpectrumCombo.SelectedIndex = SpectrumCombo.Items.Count == 1 ? 0 : -1;
            SetMode(false);
        }

        public static void ShowWindow(string? sourcePath = null)
        {
            LumFourColorCalibrationWorkflowWindow? existing = Application.Current.Windows
                .OfType<LumFourColorCalibrationWorkflowWindow>()
                .FirstOrDefault();
            if (existing != null)
            {
                if (!existing.busy && !string.IsNullOrWhiteSpace(sourcePath))
                {
                    existing.sourceFromTemplate = false;
                    existing.SourcePathBox.Text = sourcePath;
                }
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
            {
                sourceFromTemplate = false;
                if (string.Equals(SourcePathBox.Text, dialog.FileName, StringComparison.OrdinalIgnoreCase))
                    ReloadSource();
                else
                    SourcePathBox.Text = dialog.FileName;
            }
        }

        private void SourcePathBox_TextChanged(object sender, TextChangedEventArgs e) => ReloadSource();

        private void ReloadSource()
        {
            if (SourceStateText == null)
                return;
            CancelDrawMode();
            InvalidateCalculation();
            sourceSnapshot = null;
            foreach (var sample in session.Samples)
                sample.ClearCamera();
            try
            {
                if (string.IsNullOrWhiteSpace(SourcePathBox.Text))
                    SourceStateText.Text = "选择校正模板可带入四色文件，也可手动选择文件。";
                else
                {
                    sourceSnapshot = LumFourColorSourceSnapshot.Load(SourcePathBox.Text.Trim());
                    SourceStateText.Text = "文件已读取 · 更换文件会清除相机数据 · 结果另存副本";
                }
            }
            catch (Exception ex)
            {
                SourceStateText.Text = ex.Message;
            }
            RefreshSelectedSample();
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
            foreach (var sample in session.Samples)
                sample.Changed -= Sample_Changed;
            CancelDrawMode();
            session.SetMode(singlePoint);
            foreach (var sample in session.Samples)
                sample.Changed += Sample_Changed;
            InvalidateCalculation();
            SampleList.SelectedIndex = 0;
            StatusText.Text = singlePoint ? "完成一次相机与光谱采集，顺序不限。" : "按现场顺序完成 R、G、B、W。";
            RefreshSelectedSample();
        }

        private void CameraCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CalibrationCombo == null)
                return;
            if (sourceFromTemplate) SourcePathBox.Text = string.Empty;
            sourceFromTemplate = false;
            DeviceCamera? camera = CameraCombo.SelectedItem as DeviceCamera;
            CalibrationCombo.ItemsSource = camera?.PhyCamera?.CalibrationParams;
            // A template must be selected deliberately; the first item may belong to a different calibration group.
            CalibrationCombo.SelectedIndex = -1;
            ClearCameraSamples();
        }

        private void CalibrationCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ClearCameraSamples();
            if (CameraCombo.SelectedItem is not DeviceCamera camera || CalibrationCombo.SelectedItem is not TemplateModel<CalibrationParam> template) return;
            try
            {
                string? path = ResolveTemplateSource(camera, template.Value);
                if (path != null)
                {
                    sourceFromTemplate = true;
                    if (string.Equals(SourcePathBox.Text, path, StringComparison.OrdinalIgnoreCase)) ReloadSource();
                    else SourcePathBox.Text = path;
                    if (sourceSnapshot != null) SourceStateText.Text = $"已从模板「{template.Key}」带入四色校正文件 · 可重新选择文件";
                }
                else
                {
                    if (sourceFromTemplate) SourcePathBox.Text = string.Empty;
                    sourceFromTemplate = false;
                    StatusText.Text = "该模板未配置四色校正文件，可手动选择原文件。";
                }
            }
            catch (Exception ex)
            {
                if (sourceFromTemplate) SourcePathBox.Text = string.Empty;
                sourceFromTemplate = false;
                ShowError(ex.Message);
            }
        }

        internal static string? ResolveTemplateSource(DeviceCamera camera, CalibrationParam template)
        {
            if (string.IsNullOrWhiteSpace(template.Color.LumFourColor.FilePath)) return null;
            var resource = camera.GetCalibrationTemplateResource(template, CalibrationSlotDefinitions.ByKey[nameof(GroupResource.LumFourColor)]);
            if (resource == null || !camera.TryResolveCalibrationFilePath(resource, out string path, out _))
                throw new InvalidOperationException("模板的四色校正文件不存在或尚未同步，请检查模板，或手动选择文件。");
            return path;
        }

        private void ClearCameraSamples()
        {
            if (CieImageView == null)
                return;
            CancelDrawMode();
            foreach (var sample in session.Samples)
                sample.ClearCamera();
            InvalidateCalculation();
            RefreshSelectedSample();
        }

        private void SpectrumCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SpectrumGrid == null)
                return;
            foreach (var sample in session.Samples)
                sample.ClearSpectrum();
            InvalidateCalculation();
            RefreshSelectedSample();
        }

        private void Sample_Changed(object? sender, EventArgs e)
        {
            InvalidateCalculation();
            RefreshActions();
        }

        private void RestoreReference_Click(object sender, RoutedEventArgs e)
        {
            if (busy || SelectedSample?.CanRestoreReference != true) return;
            SelectedSample.RestoreReference();
            StatusText.Text = "已恢复原始参考值，请重新计算校正。";
        }

        private void SampleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CancelDrawMode();
            RefreshSelectedSample();
            if (!busy && SelectedSample is { HasImage: true, HasCameraMeasurement: false }) BeginDrawMode();
        }

        private async void CaptureCamera_Click(object sender, RoutedEventArgs e)
        {
            if (busy || sourceSnapshot == null)
                return;
            LumFourColorCalibrationSample? sample = SelectedSample;
            if (sample == null || CameraCombo.SelectedItem is not DeviceCamera camera
                || CalibrationCombo.SelectedItem is not TemplateModel<CalibrationParam> calibration)
            {
                ShowError("请选择相机和校正模板。");
                return;
            }

            await RunCaptureAsync($"正在采集 {sample.Name} 的 CIE 图像...", async token =>
            {
                sourceSnapshot.EnsureUnchanged();
                sample.ClearCamera();
                RefreshSelectedSample();
                LumFourColorCieCapture frame = await new LocalLumFourColorCameraCaptureProvider(camera, calibration.Value)
                    .CaptureAsync(sample.Target, token);
                token.ThrowIfCancellationRequested();
                sample.SetFrame(frame, LumFourColorCieService.Render(frame));
                InvalidateCalculation();
                StatusText.Text = sample.HasSpectrumMeasurement
                    ? $"{sample.Name} 取图完成，请绘制 POI；光谱数据已保留。"
                    : $"{sample.Name} 取图完成，请绘制 POI。";
                RefreshSelectedSample();
                BeginDrawMode();
            });
        }

        private void LoadCie_Click(object sender, RoutedEventArgs e)
        {
            if (busy)
                return;
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
                CancelDrawMode();
                sample.ClearCamera();
                RefreshSelectedSample();
                LumFourColorCieCapture frame = LumFourColorCieService.Load(dialog.FileName);
                sample.SetFrame(frame, LumFourColorCieService.Render(frame));
                InvalidateCalculation();
                StatusText.Text = sample.HasSpectrumMeasurement
                    ? $"已加载 {sample.Name} 的 CIE 图像，请绘制 POI；光谱数据已保留。"
                    : $"已加载 {sample.Name} 的 CIE 图像，请绘制 POI。";
                RefreshSelectedSample();
                BeginDrawMode();
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

            BeginDrawMode();
        }

        private void BeginDrawMode() => poiEditor?.Begin(poiOptions.UseRectangle);

        private void PoiShapeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PoiShapeCombo == null) return;
            poiOptions.UseRectangle = PoiShapeCombo.SelectedIndex == 1;
            if (!busy) BeginDrawMode();
        }

        private async void CaptureSpectrum_Click(object sender, RoutedEventArgs e)
        {
            if (busy)
                return;
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
                sample.ClearSpectrum();
                RefreshSelectedSample();
                LumFourColorSpectrumCapture result = await new DeviceLumFourColorSpectrumCaptureProvider(spectrum)
                    .CaptureAsync(sample.Target, token);
                token.ThrowIfCancellationRequested();
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
            if (busy || closed)
                return;
            CancelDrawMode();
            InvalidateCalculation();
            captureCancellation?.Dispose();
            captureCancellation = new CancellationTokenSource();
            SetBusy(true, message);
            try
            {
                await action(captureCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                if (!closed) StatusText.Text = "操作已取消。";
            }
            catch (Exception ex)
            {
                if (!closed) ShowError(ex.Message);
            }
            finally
            {
                if (!closed) SetBusy(false, null);
            }
        }

        private void Calculate_Click(object sender, RoutedEventArgs e)
        {
            if (busy) return;
            InvalidateCalculation();
            try
            {
                if (sourceSnapshot == null)
                    throw new InvalidOperationException("请先选择有效的原四色校正文件。");
                sourceSnapshot.EnsureUnchanged();
                if (!session.IsComplete)
                    throw new InvalidOperationException("请先完成全部色块的相机 POI 和光谱数据。");
                string[] warnings = session.Samples.SelectMany(sample => sample.GetWarnings(sourceSnapshot.Hash)).ToArray();
                if (warnings.Length > 0 && MessageBox.Show(this,
                    string.Join(Environment.NewLine + Environment.NewLine, warnings) + "\n\n这些数据可能导致错误的校正结果。建议取消并重新采集。仍要使用本次数据继续计算吗？",
                    "校正数据待复核", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                {
                    StatusText.Text = "已取消计算，请处理待复核的数据。";
                    return;
                }
                sourceSnapshot.EnsureUnchanged();
                correctedConfig = session.Calculate(sourceSnapshot.Config);
                SaveButton.IsEnabled = true;
                StatusText.Text = warnings.Length == 0 ? "校正计算完成，可以另存文件。" : "已按本次确认继续计算（含待复核数据），可另存副本。";
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
                sourceSnapshot!.SaveCopy(dialog.FileName, correctedConfig);
                StatusText.Text = $"已保存：{dialog.FileName}";
            }
            catch (Exception ex)
            {
                ShowError($"保存失败：{ex.Message}");
            }
        }

        private void RefreshSelectedSample()
        {
            if (MeasurementPanel == null || SampleList == null)
                return;
            LumFourColorCalibrationSample? sample = SelectedSample;
            MeasurementPanel.DataContext = sample;
            poiEditor?.ShowSample(sample);
            CurrentTargetText.Text = sample == null ? "相机图像" : $"{sample.Name} · 相机图像与 POI";
            CameraQualityText.Text = sample?.Frame == null ? "取图后直接框选；选择文件时请核对色块。" :
                sample.Frame.CalibrationHash == null ? "模板待核对：CVCIE 未记录所用校正文件。" :
                sample.Frame.CalibrationHash == sourceSnapshot?.Hash ? "图像所用四色校正文件与原文件一致。" : "模板不一致：当前图像使用了另一份四色校正文件。";
            if (sample?.Frame != null) CameraQualityText.Text += " 请核对原图曝光。";
            EmptyImagePanel.Visibility = sample?.HasImage == true ? Visibility.Collapsed : Visibility.Visible;
            SpectrumGrid.ItemsSource = sample?.Spectrum;
            SpectrumCountText.Text = sample == null ? string.Empty : $"{sample.Spectrum.Count} 点";
            RefreshActions();
        }

        private void RefreshActions()
        {
            if (CaptureCameraButton == null)
                return;

            LumFourColorCalibrationSample? sample = SelectedSample;
            CaptureCameraButton.IsEnabled = !busy && sourceSnapshot != null && sample != null && CameraCombo.SelectedItem != null && CalibrationCombo.SelectedItem != null;
            LoadCieButton.IsEnabled = !busy && sourceSnapshot != null && sample != null;
            DrawPoiButton.IsEnabled = !busy && sample?.HasImage == true;
            CaptureSpectrumButton.IsEnabled = !busy && sample != null && SpectrumCombo.SelectedItem != null;
            SelectSpectrumButton.IsEnabled = !busy && sample != null && SpectrumCombo.SelectedItem != null;
            ReferenceInputsPanel.IsEnabled = !busy && sample != null;
            NextSampleButton.IsEnabled = !busy && session.Samples.Any(item => item != sample && !item.IsComplete);
            CalculateButton.IsEnabled = !busy && session.IsComplete && sourceSnapshot != null;
            SaveButton.IsEnabled = !busy && correctedConfig != null;
            int complete = session.Samples.Count(item => item.IsComplete);
            ProgressText.Text = $"{complete} / {session.Samples.Count} 组数据齐全" +
                (sourceSnapshot == null ? " · 请选择有效原文件" : session.IsComplete ? " · 可计算，待复核项会先提示" : " · 继续完成缺少的相机 / 光谱");
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
            BrowseSourceButton.IsEnabled = !value;
            SourcePathBox.IsEnabled = !value;
            CieImageView.IsEnabled = !value;
            PoiShapeCombo.IsEnabled = !value;
            if (message != null)
                StatusText.Text = message;
            RefreshActions();
            if (!value && SelectedSample is { HasImage: true, HasCameraMeasurement: false }) BeginDrawMode();
        }

        private void CancelDrawMode() => poiEditor?.Cancel();

        private void InvalidateCalculation()
        {
            bool hadResult = correctedConfig != null;
            correctedConfig = null;
            if (SaveButton != null) SaveButton.IsEnabled = false;
            if (hadResult && StatusText != null) StatusText.Text = "数据已变化，请重新计算。";
        }

        private void OpenManual_Click(object sender, RoutedEventArgs e) => LumFourColorCorrectionWindow.ShowWindow(SourcePathBox.Text.Trim());

        private void ShowError(string message)
        {
            InvalidateCalculation();
            StatusText.Text = message;
        }

        private void NextSample_Click(object sender, RoutedEventArgs e)
        {
            var next = session.Samples.FirstOrDefault(item => item != SelectedSample && !item.IsComplete);
            if (next != null) SampleList.SelectedItem = next;
        }

        private async void SelectSpectrum_Click(object sender, RoutedEventArgs e)
        {
            if (busy || SelectedSample is not LumFourColorCalibrationSample sample || SpectrumCombo.SelectedItem is not DeviceSpectrum device)
                return;
            SetBusy(true, "正在读取所选光谱仪的历史数据…");
            CancelDrawMode();
            try
            {
                var results = await device.GetRecentColorMeasurementsAsync();
                if (closed) return;
                var dialog = new LumFourColorSpectrumSelectionWindow(device.Name, sample.Name, results) { Owner = this };
                if (dialog.ShowDialog() != true || dialog.SelectedResult == null) return;
                sample.ClearSpectrum();
                RefreshSelectedSample();
                var result = await device.LoadColorMeasurementAsync(dialog.SelectedResult.ResultId);
                if (closed) return;
                sample.SetSpectrumMeasurement(LumFourColorSpectrumCapture.FromMeasurement(result));
                StatusText.Text = $"已将光谱结果 {result.ResultId} 配对到 {sample.Name}，请核对现场色块与采集时间。";
                RefreshSelectedSample();
            }
            catch (Exception ex)
            {
                if (!closed) ShowError(ex.Message);
            }
            finally
            {
                if (!closed) SetBusy(false, null);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Closed(object? sender, EventArgs e)
        {
            closed = true;
            poiEditor?.Dispose();
            foreach (var sample in session.Samples) sample.Changed -= Sample_Changed;
            captureCancellation?.Cancel();
            captureCancellation?.Dispose();
        }
    }
}
