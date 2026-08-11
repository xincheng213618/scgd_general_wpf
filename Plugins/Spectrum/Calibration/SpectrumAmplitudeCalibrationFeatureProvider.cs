using ColorVision.UI;
using cvColorVision;
using log4net;
using Spectrum.Configs;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using PluginSpectrometerType = Spectrum.SpectrometerType;

namespace Spectrum.Calibration
{
    public sealed class SpectrumAmplitudeCalibrationFeatureProvider : ISpectrometerFeatureProvider
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SpectrumAmplitudeCalibrationFeatureProvider));

        public SpectrometerFeatureMetadata Metadata { get; } = new(
            "spectrum.amplitude-calibration",
            "幅值标定",
            "采集暗、亮数据并生成幅值标定文件",
            10,
            true,
            false);

        public async Task<SpectrometerFeatureResult> ExecuteAsync(
            SpectrometerConfigurationSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            SpectrometerManager manager = SpectrometerManager.Instance;
            MeasurementAdmissionPause? measurementPause = null;
            string activeGroupName = snapshot.ActiveCalibrationGroupName;

            try
            {
                if (snapshot.ContractVersion != 1)
                    return SpectrometerFeatureResult.Failure($"不支持的光谱仪功能契约版本: {snapshot.ContractVersion}", activeGroupName);

                cancellationToken.ThrowIfCancellationRequested();

                MainWindow mainWindow = await ShowSpectrumMainWindowAsync(cancellationToken).ConfigureAwait(false);

                measurementPause = manager.StopAcceptingMeasurements();
                await measurementPause.WhenDrained.WaitAsync(cancellationToken).ConfigureAwait(false);

                CalibrationGroupConfig calibrationConfig = CreateCalibrationConfig(snapshot);
                activeGroupName = calibrationConfig.ActiveGroupName;

                await InvokeOnUiAsync(() =>
                {
                    SynchronizeDeviceConfig(manager.Config, snapshot);
                    ApplyCalibrationConfig(manager, calibrationConfig, snapshot);
                    ConfigService.Instance.Save<SpectrumConfig>();
                }, cancellationToken).ConfigureAwait(false);

                SaveCalibrationConfig(calibrationConfig, snapshot.SerialNumber);

                await MainWindow.EnsureCvCameraResourceInitializedAsync()
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (!manager.IsConnected)
                {
                    int connectResult = await manager.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    if (connectResult != 1 || !manager.IsConnected)
                    {
                        string message = Spectrometer.GetErrorMessage(connectResult);
                        return SpectrometerFeatureResult.Failure(
                            string.IsNullOrWhiteSpace(message) ? "光谱仪连接失败" : $"光谱仪连接失败: {message}",
                            activeGroupName);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                string actualSerialNumber = manager.SerialNumber;
                if (!string.IsNullOrWhiteSpace(actualSerialNumber))
                {
                    SaveCalibrationConfig(calibrationConfig, actualSerialNumber);
                    if (!string.Equals(actualSerialNumber, snapshot.SerialNumber, StringComparison.OrdinalIgnoreCase))
                    {
                        log.Info($"主程序光谱仪 SN '{snapshot.SerialNumber}' 与直连设备 SN '{actualSerialNumber}' 不同，已按实际 SN 同步标定组。");
                    }
                }

                await InvokeOnUiAsync(
                    () => ApplyCalibrationConfig(manager, calibrationConfig, snapshot),
                    cancellationToken).ConfigureAwait(false);
                await ReloadCalibrationFilesAsync(manager, cancellationToken).ConfigureAwait(false);

                await ShowAmplitudeWindowAsync(mainWindow, cancellationToken).ConfigureAwait(false);
                activeGroupName = manager.CalibrationGroupConfig.ActiveGroupName;
                return SpectrometerFeatureResult.Success(string.Empty, activeGroupName);
            }
            catch (OperationCanceledException)
            {
                return SpectrometerFeatureResult.Cancel("幅值标定已取消", activeGroupName);
            }
            catch (Exception ex)
            {
                log.Error("执行幅值标定扩展失败", ex);
                return SpectrometerFeatureResult.Failure(ex.GetBaseException().Message, activeGroupName);
            }
            finally
            {
                measurementPause?.Dispose();
            }
        }

        private static CalibrationGroupConfig CreateCalibrationConfig(SpectrometerConfigurationSnapshot snapshot)
        {
            var config = new CalibrationGroupConfig();
            int unnamedGroupIndex = 1;

            foreach (SpectrometerCalibrationGroupSnapshot source in snapshot.CalibrationGroups)
            {
                string groupName = string.IsNullOrWhiteSpace(source.GroupName)
                    ? $"Group{unnamedGroupIndex++}"
                    : source.GroupName;
                config.Groups.Add(new CalibrationGroup
                {
                    GroupName = groupName,
                    WavelengthFile = ResolvePath(source.WavelengthFile, snapshot.SourceBaseDirectory),
                    MaguideFile = ResolvePath(source.MagnitudeFile, snapshot.SourceBaseDirectory),
                    FilterWheelPosition = source.FilterWheelPosition,
                });
            }

            if (config.Groups.Count == 0)
                config.Groups.Add(new CalibrationGroup { GroupName = "Default" });

            CalibrationGroup activeGroup = config.Groups.FirstOrDefault(group =>
                string.Equals(group.GroupName, snapshot.ActiveCalibrationGroupName, StringComparison.OrdinalIgnoreCase))
                ?? config.Groups[0];
            config.ActiveGroupName = activeGroup.GroupName;
            return config;
        }

        private static string ResolvePath(string path, string sourceBaseDirectory)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            if (Path.IsPathFullyQualified(path))
                return Path.GetFullPath(path);

            string baseDirectory = string.IsNullOrWhiteSpace(sourceBaseDirectory)
                ? AppContext.BaseDirectory
                : Path.GetFullPath(sourceBaseDirectory);
            return Path.GetFullPath(Path.Combine(baseDirectory, path));
        }

        private static void SynchronizeDeviceConfig(SpectrumConfig config, SpectrometerConfigurationSnapshot snapshot)
        {
            config.SpectrometerType = (PluginSpectrometerType)(int)snapshot.SpectrometerType;
            config.IsComPort = snapshot.IsComPort;
            config.SzComName = snapshot.ComPortName;
            config.BaudRate = snapshot.BaudRate;
        }

        private static void ApplyCalibrationConfig(
            SpectrometerManager manager,
            CalibrationGroupConfig calibrationConfig,
            SpectrometerConfigurationSnapshot snapshot)
        {
            manager.CalibrationGroupConfig = calibrationConfig;
            if (snapshot.IntegrationTime > 0)
                manager.IntTime = snapshot.IntegrationTime;
            if (snapshot.Average > 0)
                manager.Average = snapshot.Average;

            CalibrationGroup? activeGroup = calibrationConfig.ActiveGroup;
            if (activeGroup == null)
                return;

            manager.WavelengthFile = activeGroup.WavelengthFile;
            manager.MaguideFile = activeGroup.MaguideFile;
        }

        private static void SaveCalibrationConfig(CalibrationGroupConfig calibrationConfig, string serialNumber)
        {
            if (!string.IsNullOrWhiteSpace(serialNumber))
                calibrationConfig.Save(serialNumber);
        }

        private static async Task ReloadCalibrationFilesAsync(
            SpectrometerManager manager,
            CancellationToken cancellationToken)
        {
            (int wavelength, int magnitude) = await manager.RunExclusiveAsync(token => Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                if (!manager.IsConnected || manager.Handle == IntPtr.Zero)
                    return (-1, -1);

                int wavelengthResult = Spectrometer.CM_Emission_LoadWavaLengthFile(manager.Handle, manager.WavelengthFile);
                int magnitudeResult = string.IsNullOrWhiteSpace(manager.MaguideFile)
                    ? 1
                    : Spectrometer.CM_Emission_LoadMagiudeFile(manager.Handle, manager.MaguideFile);
                return (wavelengthResult, magnitudeResult);
            }, CancellationToken.None), cancellationToken).ConfigureAwait(false);

            if (wavelength != 1)
                log.Warn($"加载当前波长标定文件失败，可在 Spectrum 窗口中重新选择: {Spectrometer.GetErrorMessage(wavelength)}");

            if (magnitude != 1)
                log.Warn($"加载当前幅值标定文件失败，可继续生成新文件: {Spectrometer.GetErrorMessage(magnitude)}");
        }

        private static async Task<MainWindow> ShowSpectrumMainWindowAsync(CancellationToken cancellationToken)
        {
            Dispatcher dispatcher = Application.Current?.Dispatcher
                ?? throw new InvalidOperationException("当前没有可用的 WPF Dispatcher。");

            if (dispatcher.CheckAccess())
                return ShowSpectrumMainWindow();

            return await dispatcher.InvokeAsync(
                ShowSpectrumMainWindow,
                DispatcherPriority.Normal,
                cancellationToken);
        }

        private static MainWindow ShowSpectrumMainWindow()
        {
            if (MainWindow.Instance is { IsLoaded: true } existingWindow)
            {
                if (existingWindow.WindowState == WindowState.Minimized)
                    existingWindow.WindowState = WindowState.Normal;
                existingWindow.Activate();
                return existingWindow;
            }

            var window = new MainWindow();
            window.Show();
            return window;
        }

        private static async Task ShowAmplitudeWindowAsync(
            MainWindow mainWindow,
            CancellationToken cancellationToken)
        {
            Dispatcher dispatcher = Application.Current?.Dispatcher
                ?? throw new InvalidOperationException("当前没有可用的 WPF Dispatcher。");

            if (dispatcher.CheckAccess())
            {
                ShowAmplitudeWindow(mainWindow, cancellationToken);
                return;
            }

            await dispatcher.InvokeAsync(
                () => ShowAmplitudeWindow(mainWindow, cancellationToken),
                DispatcherPriority.Normal,
                cancellationToken);
        }

        private static void ShowAmplitudeWindow(
            MainWindow mainWindow,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!mainWindow.IsLoaded)
                throw new InvalidOperationException("Spectrum 主窗口已关闭，无法打开幅值标定窗口。");

            var window = new GenerateAmplitudeWindow
            {
                Owner = mainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            window.ShowDialog();
        }

        private static async Task InvokeOnUiAsync(Action action, CancellationToken cancellationToken)
        {
            Dispatcher dispatcher = Application.Current?.Dispatcher
                ?? throw new InvalidOperationException("当前没有可用的 WPF Dispatcher。");

            if (dispatcher.CheckAccess())
            {
                cancellationToken.ThrowIfCancellationRequested();
                action();
                return;
            }

            await dispatcher.InvokeAsync(action, DispatcherPriority.Normal, cancellationToken);
        }
    }
}
