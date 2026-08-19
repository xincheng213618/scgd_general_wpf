#pragma warning disable CA1822,CS0618
using ColorVision.UI.Menus;
using cvColorVision;
using ScottPlot;
using Spectrum.Menus;
using System.Windows;

namespace Spectrum.Calibration
{
    public class MenuGenerateAmplitudeWindow : SpectrumMenuIBase
    {

        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => "生成幅值标定文件";
        public override int Order => 1;
        public override void Execute()
        {
            new GenerateAmplitudeWindow().ShowDialog();
        }
    }


    public partial class GenerateAmplitudeWindow : Window, IDisposable
    {
        private SpectrometerManager Manager => SpectrometerManager.Instance;
        private readonly CancellationTokenSource windowLifetimeCancellation = new();
        private double[]? _cachedXs;
        private bool closed;
        private bool disposed;

        public GenerateAmplitudeWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Manager;
            InitializeChart();
            RefreshChart();

            Manager.DataAcquired += OnDataAcquired;
        }

        private void InitializeChart()
        {
            string title = "暗数据 / 亮数据 预览";
            AmplitudePlot.Plot.Title(title);
            AmplitudePlot.Plot.XLabel("像素点");
            AmplitudePlot.Plot.YLabel("强度");

            string fontSample = "暗数据 / 亮数据 预览";
            AmplitudePlot.Plot.Axes.Title.Label.FontName = Fonts.Detect(fontSample);
            AmplitudePlot.Plot.Axes.Left.Label.FontName = Fonts.Detect(fontSample);
            AmplitudePlot.Plot.Axes.Bottom.Label.FontName = Fonts.Detect(fontSample);
        }

        private void RefreshChart()
        {
            AmplitudePlot.Plot.Clear();

            int len = Manager.fDarkData.Length;
            if (_cachedXs == null || _cachedXs.Length != len)
            {
                _cachedXs = new double[len];
                for (int i = 0; i < len; i++)
                    _cachedXs[i] = i;
            }

            // Plot dark data
            double[] darkYs = new double[len];
            bool hasDark = false;
            for (int i = 0; i < len; i++)
            {
                darkYs[i] = Manager.fDarkData[i];
                if (!hasDark && Manager.fDarkData[i] != 0) hasDark = true;
            }
            if (hasDark)
            {
                var darkPlot = AmplitudePlot.Plot.Add.Scatter(_cachedXs, darkYs);
                darkPlot.Label = "暗数据";
                darkPlot.Color = ScottPlot.Color.FromColor(System.Drawing.Color.DodgerBlue);
                darkPlot.LineWidth = 1;
                darkPlot.MarkerSize = 0;
            }

            // Plot light data
            double[] lightYs = new double[len];
            bool hasLight = false;
            for (int i = 0; i < len; i++)
            {
                lightYs[i] = Manager.fLightData[i];
                if (!hasLight && Manager.fLightData[i] != 0) hasLight = true;
            }
            if (hasLight)
            {
                var lightPlot = AmplitudePlot.Plot.Add.Scatter(_cachedXs, lightYs);
                lightPlot.Label = "亮数据";
                lightPlot.Color = ScottPlot.Color.FromColor(System.Drawing.Color.OrangeRed);
                lightPlot.LineWidth = 1;
                lightPlot.MarkerSize = 0;
            }

            AmplitudePlot.Plot.ShowLegend();
            AmplitudePlot.Plot.Axes.AutoScale();
            AmplitudePlot.Refresh();

            // Update status
            string darkStatus = hasDark ? "✓" : "✗";
            string lightStatus = hasLight ? "✓" : "✗";
            StatusText.Text = $"暗数据: {darkStatus}  |  亮数据: {lightStatus}";
        }

        private void OnDataAcquired(object? sender, EventArgs e)
        {
            if (closed || Dispatcher.HasShutdownStarted)
                return;
            if (Dispatcher.CheckAccess())
                RefreshChart();
            else
                _ = Dispatcher.BeginInvoke(RefreshChart);
        }

        private void SelectCsFile_Click(object sender, RoutedEventArgs e)
        {
            using System.Windows.Forms.OpenFileDialog dialog = new()
            {
                Filter = "All Files|*.*",
                RestoreDirectory = true
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                Manager.CSFile = dialog.FileName;
        }

        private void SelectMagnitudeOutputFile_Click(object sender, RoutedEventArgs e)
        {
            string? path = SelectMagnitudeOutputPath();
            if (path != null)
                Manager.MaguideFileOutput = path;
        }

        private string? SelectMagnitudeOutputPath()
        {
            using System.Windows.Forms.SaveFileDialog dialog = new()
            {
                FileName = $"Magiude_{DateTime.Now:yyyyMMdd_HHmmss}.dat",
                Filter = "DAT files (*.dat)|*.dat|All files (*.*)|*.*",
                Title = "选择幅值标定文件保存路径",
                RestoreDirectory = true
            };
            return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.FileName : null;
        }

        private async void CaptureDarkData_Click(object sender, RoutedEventArgs e) =>
            await CaptureCalibrationDataAsync(sender as System.Windows.Controls.Button, captureDark: true);

        private async void CaptureLightData_Click(object sender, RoutedEventArgs e) =>
            await CaptureCalibrationDataAsync(sender as System.Windows.Controls.Button, captureDark: false);

        private async Task CaptureCalibrationDataAsync(System.Windows.Controls.Button? button, bool captureDark)
        {
            if (button != null)
                button.IsEnabled = false;
            StatusText.Text = captureDark ? "正在获取暗数据…" : "正在获取亮数据…";
            try
            {
                int result = captureDark
                    ? await Manager.CaptureDarkDataAsync(windowLifetimeCancellation.Token)
                    : await Manager.CaptureLightDataAsync(windowLifetimeCancellation.Token);
                if (closed)
                    return;

                string operation = captureDark ? "暗数据" : "亮数据";
                if (result == 1)
                {
                    StatusText.Text = $"{operation}获取成功";
                    MessageBox.Show(this, $"{operation}获取成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    string error = Spectrometer.GetErrorMessage(result);
                    StatusText.Text = $"{operation}获取失败：{error}";
                    MessageBox.Show(this, StatusText.Text, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (OperationCanceledException) when (windowLifetimeCancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                StatusText.Text = $"操作失败：{ex.GetBaseException().Message}";
                MessageBox.Show(this, StatusText.Text, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                if (button != null && !closed)
                    button.IsEnabled = true;
            }
        }

        private async void GenerateAmplitude_Click(object sender, RoutedEventArgs e)
        {
            string outputPath = Manager.MaguideFileOutput;
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                string? selectedPath = SelectMagnitudeOutputPath();
                if (selectedPath == null)
                    return;
                outputPath = selectedPath;
                Manager.MaguideFileOutput = outputPath;
            }

            System.Windows.Controls.Button? button = sender as System.Windows.Controls.Button;
            if (button != null)
                button.IsEnabled = false;
            StatusText.Text = "正在采集并生成幅值标定文件…";
            try
            {
                (int captureResult, int generateResult) = await Manager
                    .GenerateAmplitudeAsync(outputPath, windowLifetimeCancellation.Token);
                if (closed)
                    return;

                if (captureResult != 1)
                {
                    string error = Spectrometer.GetErrorMessage(captureResult);
                    StatusText.Text = $"获取亮数据失败：{error}";
                    MessageBox.Show(this, StatusText.Text, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (generateResult == 1)
                {
                    StatusText.Text = $"生成成功：{outputPath}";
                    MessageBox.Show(this, $"生成成功\n文件：{outputPath}", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    string error = Spectrometer.GetErrorMessage(generateResult);
                    StatusText.Text = $"生成失败：{error}";
                    MessageBox.Show(this, StatusText.Text, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (OperationCanceledException) when (windowLifetimeCancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                StatusText.Text = $"生成失败：{ex.GetBaseException().Message}";
                MessageBox.Show(this, StatusText.Text, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                if (button != null && !closed)
                    button.IsEnabled = true;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            Dispose();
            base.OnClosed(e);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            closed = true;
            windowLifetimeCancellation.Cancel();
            Manager.DataAcquired -= OnDataAcquired;
            windowLifetimeCancellation.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
