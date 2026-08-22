#pragma warning disable CA1051,CA1805,CA1806,CA1822
using ColorVision.Common.MVVM;
using ColorVision.UI;
using cvColorVision;
using log4net;
using Newtonsoft.Json;
using SpectrumResources = Spectrum.Properties.Resources;
using Spectrum.Configs;
using Spectrum.Data;
using Spectrum.License;
using Spectrum.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

#pragma warning disable CA1001 // App-lifetime singleton owns the device gate.

namespace Spectrum
{
    [DisplayName("EmissionSP100设置")]
    public class SetEmissionSP100Config : ViewModelBase, IConfig
    {
        public static SetEmissionSP100Config Instance => ConfigService.Instance.GetRequiredService<SetEmissionSP100Config>();

        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; OnPropertyChanged(); } }
        private bool _IsEnabled = true;

        public int nStartPos { get => _nStartPos; set { _nStartPos = value; OnPropertyChanged(); } }
        private int _nStartPos = 1691;

        public int nEndPos { get => _nEndPos; set { _nEndPos = value; OnPropertyChanged(); } }
        private int _nEndPos = 2048;

        public double dMeanThreshold { get => _dMeanThreshold; set { _dMeanThreshold = value; OnPropertyChanged(); } }
        private double _dMeanThreshold = 80;
    }
    [DisplayName("自动积分时间配置")]
    public class IntTimeConfig : ViewModelBase, IConfig
    {
        [DisplayName("积分时间上限Ms")]
        public int IntLimitTime { get => _IntLimitTime; set { _IntLimitTime = value; OnPropertyChanged(); } }
        private int _IntLimitTime = 6000;

        [DisplayName("自动积分时间起始")]
        public float AutoIntTimeB { get => _AutoIntTimeB; set { _AutoIntTimeB = value; OnPropertyChanged(); } }
        private float _AutoIntTimeB = 1;

        [DisplayName("自动积分阈值(%)")]
        public double MaxPercent { get => _MaxPercent; set { _MaxPercent = value; OnPropertyChanged(); Max = (int)(_MaxPercent * 655.35); } } // 655.35 = 65535 / 100, converts percentage to 16-bit ADC scale
        private double _MaxPercent = 76.3;

        [Browsable(false)]
        public int Max { get => _Max; set { _Max = value; OnPropertyChanged(); } }
        private int _Max = 50000;

        [DisplayName("旧版本模式")]
        public bool IsOldVersion { get => _IsOldVersion; set { _IsOldVersion = value; OnPropertyChanged(); } }
        private bool _IsOldVersion = false;
    }

    public class GetDataConfig : ViewModelBase, IConfig
    {
        [DisplayName("是否开启同步频率")]
        public bool IsSyncFrequencyEnabled { get => _IsSyncFrequencyEnabled; set { _IsSyncFrequencyEnabled = value; OnPropertyChanged(); } }
        private bool _IsSyncFrequencyEnabled;

        [DisplayName("同步频率")]
        public double Syncfreq { get => _Syncfreq; set { _Syncfreq = value; OnPropertyChanged(); } }
        private double _Syncfreq = 1000;

        [DisplayName("同步频率系数")]
        public int SyncfreqFactor { get => _SyncfreqFactor; set { _SyncfreqFactor = value; OnPropertyChanged(); } }
        private int _SyncfreqFactor = 10;

        [DisplayName("滤波宽度")]
        public int FilterBW { get => _FilterBW; set { _FilterBW = value; OnPropertyChanged(); } }
        private int _FilterBW = 5;

        [DisplayName("起始波长")]
        public float SetWL1 { get => _SetWL1; set { _SetWL1 = value; OnPropertyChanged(); } }
        private float _SetWL1 = 380;

        [DisplayName("结束波长")]
        public float SetWL2 { get => _SetWL2; set { _SetWL2 = value; OnPropertyChanged(); } }
        private float _SetWL2 = 780;
    }

    [DisplayName("自动积分与数据采集配置")]
    public class MeasurementDataConfig : ViewModelBase
    {

        [DisplayName("自动积分时间配置")]
        public IntTimeConfig IntTimeConfig { get => _IntTimeConfig; set { _IntTimeConfig = value; OnPropertyChanged(); } }
        private IntTimeConfig _IntTimeConfig = new IntTimeConfig();

        [DisplayName("数据采集配置")]
        public GetDataConfig GetDataConfig { get => _GetDataConfig; set { _GetDataConfig = value; OnPropertyChanged(); } }
        private GetDataConfig _GetDataConfig = new GetDataConfig();
    }


    public class AutodarkParam : ViewModelBase,IConfig
    {
        [DisplayName("起始时间(ms)")]
        public float fTimeStart { get => _fTimeStart; set { _fTimeStart = value; OnPropertyChanged(); OnPropertyChanged(nameof(nEndTime)); } }
        private float _fTimeStart = 50f;

        [DisplayName("步进(ms)")]
        public int nStepTime { get => _nStepTime; set { _nStepTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(nEndTime)); } }
        private int _nStepTime = 100;

        [DisplayName("测量次数")]
        public int nStepCount { get => _nStepCount; set { _nStepCount = Math.Max(1, value); OnPropertyChanged(); OnPropertyChanged(nameof(nEndTime)); } }
        private int _nStepCount = 1;

        [DisplayName("结束时间(ms)")]
        public int nEndTime { get => (int)Math.Round(fTimeStart + (nStepCount - 1) * nStepTime); set => RecalculateStepCount(value); }

        private void RecalculateStepCount(int _nEndTime)
        {
            if (nStepTime > 0)
            {
                double intervals = Math.Max(0, (_nEndTime - fTimeStart) / nStepTime);
                nStepCount = (int)Math.Round(intervals) + 1;
            }
            else
            {
                nStepCount = 1;
            }
        }

        /// <summary>
        /// Action delegate set by MainWindow to execute adaptive auto dark calibration
        /// </summary>
        [JsonIgnore]
        [Browsable(false)]
        public Action ExecuteAdaptiveAutoDark { get; set; }
    }

    internal sealed class MeasurementAdmissionPause : IDisposable
    {
        private SpectrometerManager? manager;

        internal Task WhenDrained { get; }

        internal MeasurementAdmissionPause(SpectrometerManager manager, Task whenDrained)
        {
            this.manager = manager;
            WhenDrained = whenDrained;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref manager, null)?.ReleaseMeasurementPause();
        }
    }

    public sealed record SpectrumMeasurementResult(
        ViewResultSpectrum? Result,
        int? ErrorCode = null,
        string? ErrorMessage = null,
        bool IsBusy = false)
    {
        public bool IsSuccess => Result != null;
    }

    internal sealed record SpectrumCalibrationSnapshot(
        string GroupName,
        string WavelengthPath,
        string WavelengthSha256,
        string MagnitudePath,
        string MagnitudeSha256)
    {
        internal bool MatchesConfigured(string groupName, string wavelengthPath, string magnitudePath)
        {
            try
            {
                return string.Equals(GroupName, groupName, StringComparison.Ordinal)
                    && string.Equals(WavelengthPath, Path.GetFullPath(wavelengthPath), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(MagnitudePath, Path.GetFullPath(magnitudePath), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public sealed record SpectrumCalibrationApplyResult(bool IsSuccess, string ErrorMessage)
    {
        public static SpectrumCalibrationApplyResult Success { get; } = new(true, string.Empty);

        internal int RequestVersion { get; init; }
    }

    public class SpectrometerManager : ViewModelBase,IConfig
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SpectrometerManager));
        private readonly SemaphoreSlim deviceOperationGate = new(1, 1);
        private readonly object measurementStateLock = new();
        private readonly object calibrationStateLock = new();
        private int measurementPauseCount;
        private TaskCompletionSource<bool>? measurementsDrained;
        private SpectrumCalibrationSnapshot? loadedCalibration;
        private int calibrationRequestVersion;
        private bool calibrationLoadInProgress;
        private int pendingCalibrationConfigurationVersion;

        public const int CalibrationUnavailable = int.MinValue + 1;

        public SpectrumConfig Config => ConfigService.Instance.GetRequiredService<SpectrumConfig>();

        public static SpectrometerManager Instance => ConfigService.Instance.GetRequiredService<SpectrometerManager>();

        public static ViewResultManager ViewResultManager => ViewResultManager.GetInstance();

        [JsonIgnore]
        public ShutterController ShutterController { get; set; } = new ShutterController();

        [JsonIgnore]
        public FilterWheelController FilterWheelController { get; set; } = new FilterWheelController();

        [JsonIgnore]
        public SmuController SmuController { get; set; } = new SmuController();

        public static SetEmissionSP100Config SetEmissionSP100Config => SetEmissionSP100Config.Instance;

        [JsonIgnore]
        public IntPtr Handle { get; private set; } = IntPtr.Zero;

        [JsonIgnore]
        public bool IsDeviceBusy => deviceOperationGate.CurrentCount == 0;

        [JsonIgnore]
        public bool IsMeasurementActive => Volatile.Read(ref activeMeasurementCount) > 0;

        [JsonIgnore]
        public bool IsBusy => IsDeviceBusy || IsMeasurementActive || SmuController.IsBusy || ShutterController.IsBusy || FilterWheelController.IsBusy;
        private int activeMeasurementCount;

        [JsonIgnore]
        public bool IsCalibrationConfigurationPending => Volatile.Read(ref pendingCalibrationConfigurationVersion) != 0;

        [JsonIgnore]
        public bool IsCalibrationReady => IsConnected
            && !Volatile.Read(ref calibrationLoadInProgress)
            && !IsCalibrationConfigurationPending
            && loadedCalibration?.MatchesConfigured(ActiveCalibrationGroupName, WavelengthFile, MaguideFile) == true;

        [JsonIgnore]
        public string CalibrationStatus
        {
            get => _CalibrationStatus;
            private set { _CalibrationStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCalibrationReady)); }
        }
        private string _CalibrationStatus = "标定文件尚未加载";

        [JsonIgnore]
        public string LastOperationError
        {
            get => _LastOperationError;
            private set { _LastOperationError = value; OnPropertyChanged(); }
        }
        private string _LastOperationError = string.Empty;

        public const int ShutterOperationFailed = int.MinValue + 2;

        public string GetOperationErrorMessage(int resultCode)
        {
            return resultCode is CalibrationUnavailable or ShutterOperationFailed
                ? LastOperationError
                : Spectrometer.GetErrorMessage(resultCode);
        }

        internal MeasurementAdmissionPause StopAcceptingMeasurements()
        {
            lock (measurementStateLock)
            {
                measurementPauseCount++;
                Task whenDrained;
                if (activeMeasurementCount == 0)
                {
                    whenDrained = Task.CompletedTask;
                }
                else
                {
                    measurementsDrained ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    whenDrained = measurementsDrained.Task;
                }

                return new MeasurementAdmissionPause(this, whenDrained);
            }
        }

        internal void ReleaseMeasurementPause()
        {
            lock (measurementStateLock)
            {
                if (measurementPauseCount > 0)
                    measurementPauseCount--;
            }
        }

        private bool TryStartMeasurement()
        {
            lock (measurementStateLock)
            {
                if (measurementPauseCount > 0)
                    return false;

                activeMeasurementCount++;
                return true;
            }
        }

        private void FinishMeasurement()
        {
            TaskCompletionSource<bool>? drained = null;
            lock (measurementStateLock)
            {
                activeMeasurementCount--;
                if (activeMeasurementCount == 0)
                {
                    drained = measurementsDrained;
                    measurementsDrained = null;
                }
            }

            drained?.TrySetResult(true);
        }
        
        [JsonIgnore]
        public bool IsConnected { get => _IsConnected; private set { _IsConnected = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConnectionTypeDisplay)); OnPropertyChanged(nameof(HardwareModel)); OnPropertyChanged(nameof(IsCalibrationReady)); } }
        private bool _IsConnected = false;

        /// <summary>
        /// 硬件型号，连接后显示
        /// </summary>
        [JsonIgnore]
        public string HardwareModel { get => _IsConnected ? _HardwareModel : "---"; set { _HardwareModel = value; OnPropertyChanged(); } }
        private string _HardwareModel = "SP-100";

        /// <summary>
        /// 当前测量模式文本
        /// </summary>
        [JsonIgnore]
        public string MeasurementMode { get => _MeasurementMode; set { _MeasurementMode = value; OnPropertyChanged(); } }
        private string _MeasurementMode = SpectrumResources.BrightnessChromaticityMode;

        /// <summary>
        /// The serial number of the currently connected spectrometer.
        /// </summary>
        [JsonIgnore]
        public string SerialNumber { get => _SerialNumber; private set { _SerialNumber = value; OnPropertyChanged(); } }
        private string _SerialNumber = string.Empty;

        /// <summary>
        /// Readable connection type string for the status bar.
        /// </summary>
        [JsonIgnore]
        public string ConnectionTypeDisplay
        {
            get
            {
                if (!IsConnected) return SpectrumResources.未连接;
                return Config.IsComPort ? $"COM: {Config.SzComName}" : "USB";
            }
        }

        /// <summary>
        /// Calibration group config, loaded per-SN from Documents/Spectrometer/{SN}/
        /// </summary>
        [JsonIgnore]
        public CalibrationGroupConfig CalibrationGroupConfig { get => _CalibrationGroupConfig; set { _CalibrationGroupConfig = value; OnPropertyChanged(); OnPropertyChanged(nameof(CalibrationGroupNames)); } }
        private CalibrationGroupConfig _CalibrationGroupConfig = CreateDefaultCalibrationGroupConfig();

        private static CalibrationGroupConfig CreateDefaultCalibrationGroupConfig()
        {
            var config = new CalibrationGroupConfig();
            config.Groups.Add(new CalibrationGroup { GroupName = "Default" });
            return config;
        }

        /// <summary>
        /// The group names for ComboBox binding.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<string> CalibrationGroupNames => CalibrationGroupConfig.Groups.Select(g => g.GroupName);

        /// <summary>
        /// The active calibration group name. Changing this triggers file reload when connected.
        /// </summary>
        [JsonIgnore]
        public string ActiveCalibrationGroupName
        {
            get => CalibrationGroupConfig.ActiveGroupName;
            set
            {
                lock (calibrationStateLock)
                {
                    if (CalibrationGroupConfig.ActiveGroupName == value) return;
                    string previousGroupName = CalibrationGroupConfig.ActiveGroupName;
                    CalibrationGroupConfig.ActiveGroupName = value;
                    OnPropertyChanged();
                    ApplyActiveGroup(previousGroupName);
                    if (!IsConnected)
                        SaveCalibrationConfig();
                }
            }
        }

        [JsonIgnore]
        public RelayCommand ApplyActiveGroupCommand { get; set; }

        /// <summary>
        /// Loads calibration config for the current SN.
        /// </summary>
        public void LoadCalibrationConfig()
        {
            if (string.IsNullOrEmpty(SerialNumber)) return;
            CalibrationGroupConfig = CalibrationGroupConfig.Load(SerialNumber);
            OnPropertyChanged(nameof(CalibrationGroupNames));
            OnPropertyChanged(nameof(ActiveCalibrationGroupName));
            ApplyActiveGroup();
        }

        /// <summary>
        /// Saves calibration config for the current SN.
        /// </summary>
        public bool SaveCalibrationConfig()
        {
            lock (calibrationStateLock)
            {
                if (string.IsNullOrEmpty(SerialNumber))
                {
                    LastOperationError = "设备序列号未知，无法保存标定配置";
                    return false;
                }

                CalibrationGroupConfig configuration = CalibrationGroupConfig.Clone();
                if (configuration.TrySave(SerialNumber, out string errorMessage))
                    return true;

                LastOperationError = $"保存标定配置失败：{errorMessage}";
                return false;
            }
        }

        /// <summary>
        /// Applies the active calibration group: updates WavelengthFile/MaguideFile and reloads if connected.
        /// </summary>
        private void ApplyActiveGroup(string? rollbackGroupName = null)
        {
            var group = CalibrationGroupConfig.ActiveGroup;
            if (group == null) return;

            WavelengthFile = group.WavelengthFile;
            MaguideFile = group.MaguideFile;

            if (IsConnected && Handle != IntPtr.Zero)
            {
                string? lastLoadedGroupName = loadedCalibration?.GroupName;
                CalibrationGroupConfig requestedConfiguration = CalibrationGroupConfig.Clone();
                int requestVersion = BeginCalibrationRequest("正在加载标定文件…", requiresConfigurationCommit: true);
                _ = ReloadActiveGroupAsync(
                    requestVersion,
                    requestedConfiguration,
                    rollbackGroupName,
                    lastLoadedGroupName);
            }
            else
            {
                loadedCalibration = null;
                CalibrationStatus = "连接光谱仪后加载标定文件";
            }
        }

        private async Task ReloadActiveGroupAsync(
            int requestVersion,
            CalibrationGroupConfig requestedConfiguration,
            string? rollbackGroupName,
            string? lastLoadedGroupName)
        {
            CalibrationGroup requestedGroup = requestedConfiguration.ActiveGroup
                ?? throw new InvalidOperationException("标定配置中没有活动分组");
            SpectrumCalibrationApplyResult result = await ReloadCalibrationFilesAsync(
                requestVersion,
                requestedGroup.GroupName,
                requestedGroup.WavelengthFile,
                requestedGroup.MaguideFile).ConfigureAwait(false);

            if (requestVersion != Volatile.Read(ref calibrationRequestVersion))
                return;

            if (result.IsSuccess)
            {
                if (CommitCalibrationConfiguration(requestedConfiguration, requestVersion))
                    return;

                if (requestVersion != Volatile.Read(ref calibrationRequestVersion))
                    return;

                string saveError = LastOperationError;
                if (TryRestoreConfiguredCalibration(lastLoadedGroupName, requestVersion))
                {
                    SpectrumCalibrationApplyResult restore = await RestoreConfiguredCalibrationAsync(requestVersion).ConfigureAwait(false);
                    string restoreStatus = restore.IsSuccess
                        ? $"标定切换未保存，已恢复：{ActiveCalibrationGroupName}（{saveError}）"
                        : $"标定切换未保存且恢复失败：{restore.ErrorMessage}";
                    TrySetCalibrationStatus(restore.RequestVersion, restoreStatus);
                }
                else
                {
                    ClearPendingCalibrationConfiguration(requestVersion);
                    SpectrumCalibrationSnapshot? loadedSnapshot = GetLoadedCalibrationSnapshot();
                    if (loadedSnapshot != null)
                        InvalidateLoadedCalibration(loadedSnapshot, $"标定配置保存失败：{saveError}");
                    TrySetCalibrationStatus(requestVersion, $"标定已加载但配置保存失败：{saveError}");
                }
                return;
            }

            string? restoredGroupName = loadedCalibration?.GroupName ?? rollbackGroupName;
            if (TryRestoreConfiguredCalibration(restoredGroupName, requestVersion))
            {
                ClearPendingCalibrationConfiguration(requestVersion);
                string failureStatus = IsCalibrationReady
                    ? $"标定切换失败，继续使用：{restoredGroupName}"
                    : $"标定切换失败，原标定也不可用：{result.ErrorMessage}";
                TrySetCalibrationStatus(requestVersion, failureStatus);
            }
            else
            {
                ClearPendingCalibrationConfiguration(requestVersion);
            }
        }

        private bool TryRestoreConfiguredCalibration(string? groupName, int expectedRequestVersion)
        {
            lock (calibrationStateLock)
            {
                if (expectedRequestVersion != calibrationRequestVersion)
                    return false;

                CalibrationGroup? group = CalibrationGroupConfig.Groups.FirstOrDefault(item =>
                    string.Equals(item.GroupName, groupName, StringComparison.Ordinal));
                if (group == null)
                    return false;

                CalibrationGroupConfig.ActiveGroupName = group.GroupName;
                _WavelengthFile = group.WavelengthFile;
                _MaguideFile = group.MaguideFile;
                OnPropertyChanged(nameof(ActiveCalibrationGroupName));
                OnPropertyChanged(nameof(WavelengthFile));
                OnPropertyChanged(nameof(MaguideFile));
                OnPropertyChanged(nameof(IsCalibrationReady));
                return true;
            }
        }

        private int BeginCalibrationRequest(
            string status,
            bool requiresConfigurationCommit = false,
            bool rejectWhenConfigurationPending = false)
        {
            lock (calibrationStateLock)
            {
                if (rejectWhenConfigurationPending && pendingCalibrationConfigurationVersion != 0)
                    return 0;

                int requestVersion = ++calibrationRequestVersion;
                calibrationLoadInProgress = true;
                if (requiresConfigurationCommit)
                {
                    pendingCalibrationConfigurationVersion = requestVersion;
                    OnPropertyChanged(nameof(IsCalibrationConfigurationPending));
                }
                CalibrationStatus = status;
                LastOperationError = string.Empty;
                return requestVersion;
            }
        }

        private void ClearPendingCalibrationConfiguration(int requestVersion)
        {
            lock (calibrationStateLock)
            {
                if (pendingCalibrationConfigurationVersion != requestVersion)
                    return;

                pendingCalibrationConfigurationVersion = 0;
                OnPropertyChanged(nameof(IsCalibrationConfigurationPending));
                OnPropertyChanged(nameof(IsCalibrationReady));
            }
        }

        internal bool IsCalibrationRequestCurrent(int requestVersion)
        {
            lock (calibrationStateLock)
                return requestVersion != 0 && requestVersion == calibrationRequestVersion;
        }

        private bool TrySetCalibrationStatus(int expectedRequestVersion, string status)
        {
            lock (calibrationStateLock)
            {
                if (expectedRequestVersion == 0 || expectedRequestVersion != calibrationRequestVersion)
                    return false;

                CalibrationStatus = status;
                return true;
            }
        }

        public Task<SpectrumCalibrationApplyResult> ApplyConfiguredCalibrationAsync(CancellationToken cancellationToken = default)
            => ApplyCalibrationRequestAsync(
                ActiveCalibrationGroupName,
                WavelengthFile,
                MaguideFile,
                requiresConfigurationCommit: false,
                rejectWhenConfigurationPending: true,
                cancellationToken);

        internal async Task<SpectrumCalibrationApplyResult> ApplyCalibrationAsync(
            string groupName,
            string wavelengthFile,
            string magnitudeFile,
            CancellationToken cancellationToken = default)
            => await ApplyCalibrationRequestAsync(
                groupName,
                wavelengthFile,
                magnitudeFile,
                requiresConfigurationCommit: true,
                rejectWhenConfigurationPending: false,
                cancellationToken).ConfigureAwait(false);

        internal async Task<SpectrumCalibrationApplyResult> RestoreConfiguredCalibrationAsync(
            int pendingRequestVersion,
            CancellationToken cancellationToken = default)
        {
            if (!TryBeginCalibrationRestore(
                pendingRequestVersion,
                out int restoreRequestVersion,
                out string groupName,
                out string wavelengthFile,
                out string magnitudeFile))
            {
                return new SpectrumCalibrationApplyResult(false, "标定恢复请求已被更新的选择替代");
            }

            try
            {
                SpectrumCalibrationApplyResult result = await ReloadCalibrationFilesAsync(
                    restoreRequestVersion,
                    groupName,
                    wavelengthFile,
                    magnitudeFile,
                    cancellationToken).ConfigureAwait(false);
                return result with { RequestVersion = restoreRequestVersion };
            }
            finally
            {
                ClearPendingCalibrationConfiguration(pendingRequestVersion);
            }
        }

        private bool TryBeginCalibrationRestore(
            int pendingRequestVersion,
            out int restoreRequestVersion,
            out string groupName,
            out string wavelengthFile,
            out string magnitudeFile)
        {
            lock (calibrationStateLock)
            {
                if (pendingRequestVersion == 0
                    || calibrationRequestVersion != pendingRequestVersion
                    || pendingCalibrationConfigurationVersion != pendingRequestVersion)
                {
                    restoreRequestVersion = 0;
                    groupName = string.Empty;
                    wavelengthFile = string.Empty;
                    magnitudeFile = string.Empty;
                    return false;
                }

                restoreRequestVersion = ++calibrationRequestVersion;
                groupName = ActiveCalibrationGroupName;
                wavelengthFile = WavelengthFile;
                magnitudeFile = MaguideFile;
                calibrationLoadInProgress = true;
                CalibrationStatus = "正在恢复上一组标定文件…";
                LastOperationError = string.Empty;
                return true;
            }
        }

        private async Task<SpectrumCalibrationApplyResult> ApplyCalibrationRequestAsync(
            string groupName,
            string wavelengthFile,
            string magnitudeFile,
            bool requiresConfigurationCommit,
            bool rejectWhenConfigurationPending,
            CancellationToken cancellationToken)
        {
            int requestVersion = BeginCalibrationRequest(
                "正在加载标定文件…",
                requiresConfigurationCommit,
                rejectWhenConfigurationPending);
            if (requestVersion == 0)
                return new SpectrumCalibrationApplyResult(false, "标定配置正在提交，请稍候");

            try
            {
                SpectrumCalibrationApplyResult result = await ReloadCalibrationFilesAsync(
                    requestVersion,
                    groupName,
                    wavelengthFile,
                    magnitudeFile,
                    cancellationToken).ConfigureAwait(false);
                if (requiresConfigurationCommit && !result.IsSuccess)
                    ClearPendingCalibrationConfiguration(requestVersion);
                return result with { RequestVersion = requestVersion };
            }
            catch
            {
                if (requiresConfigurationCommit)
                    ClearPendingCalibrationConfiguration(requestVersion);
                throw;
            }
        }

        private async Task<SpectrumCalibrationApplyResult> ReloadCalibrationFilesAsync(
            int requestVersion,
            string groupName,
            string wavelengthFile,
            string magnitudeFile,
            CancellationToken cancellationToken = default)
        {
            SpectrumCalibrationSnapshot? previousSnapshot = loadedCalibration;
            bool candidateLoadStarted = false;
            try
            {
                if (!TryCreateCalibrationSnapshot(groupName, wavelengthFile, magnitudeFile, out SpectrumCalibrationSnapshot? candidate, out string validationError))
                {
                    SpectrumCalibrationApplyResult invalid = new(false, validationError);
                    CommitCalibrationResult(requestVersion, previousSnapshot, invalid);
                    return invalid;
                }

                SpectrumCalibrationApplyResult result = await RunExclusiveAsync(token => Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    if (requestVersion != Volatile.Read(ref calibrationRequestVersion))
                        return new SpectrumCalibrationApplyResult(false, "标定加载请求已被更新的选择替代");
                    if (!IsConnected || Handle == IntPtr.Zero)
                        return new SpectrumCalibrationApplyResult(false, "光谱仪未连接");

                    candidateLoadStarted = true;
                    SpectrumCalibrationApplyResult loadResult;
                    try
                    {
                        loadResult = LoadCalibrationFilesCore(candidate!);
                    }
                    catch (Exception ex)
                    {
                        log.Error("加载候选标定时发生异常", ex);
                        loadResult = new SpectrumCalibrationApplyResult(false, ex.GetBaseException().Message);
                    }
                    if (loadResult.IsSuccess)
                        return CompleteCalibrationLoadWithinDeviceGate(requestVersion, candidate, loadResult);

                    if (previousSnapshot != null)
                    {
                        SpectrumCalibrationApplyResult restoreResult;
                        try
                        {
                            restoreResult = LoadCalibrationFilesCore(previousSnapshot);
                        }
                        catch (Exception ex)
                        {
                            log.Error("恢复上一组标定时发生异常", ex);
                            restoreResult = new SpectrumCalibrationApplyResult(false, ex.GetBaseException().Message);
                        }
                        if (restoreResult.IsSuccess)
                        {
                            SpectrumCalibrationApplyResult restoredFailure = loadResult with
                            {
                                ErrorMessage = $"{loadResult.ErrorMessage}；已恢复上一组标定"
                            };
                            return CompleteCalibrationLoadWithinDeviceGate(requestVersion, previousSnapshot, restoredFailure);
                        }

                        SpectrumCalibrationApplyResult restoreFailed = loadResult with
                        {
                            ErrorMessage = $"{loadResult.ErrorMessage}；恢复上一组标定也失败：{restoreResult.ErrorMessage}"
                        };
                        return CompleteCalibrationLoadWithinDeviceGate(requestVersion, null, restoreFailed);
                    }

                    return CompleteCalibrationLoadWithinDeviceGate(requestVersion, null, loadResult);
                }, CancellationToken.None), cancellationToken).ConfigureAwait(false);

                return result;
            }
            catch (OperationCanceledException)
            {
                SpectrumCalibrationApplyResult cancelled = new(false, "标定文件加载已取消");
                CommitCalibrationResult(requestVersion, previousSnapshot, cancelled);
                throw;
            }
            catch (Exception ex)
            {
                log.Warn("切换校准组时重新加载标定文件失败", ex);
                SpectrumCalibrationApplyResult failed = new(false, ex.GetBaseException().Message);
                CommitCalibrationResult(requestVersion, candidateLoadStarted ? null : previousSnapshot, failed);
                return failed;
            }
        }

        private bool CommitCalibrationResult(
            int requestVersion,
            SpectrumCalibrationSnapshot? snapshot,
            SpectrumCalibrationApplyResult result)
        {
            lock (calibrationStateLock)
            {
                if (requestVersion != calibrationRequestVersion)
                    return false;

                loadedCalibration = snapshot;
                calibrationLoadInProgress = false;
                LastOperationError = result.ErrorMessage;
                if (result.IsSuccess)
                    CalibrationStatus = $"标定已加载：{snapshot!.GroupName}";
                else if (snapshot != null)
                    CalibrationStatus = $"标定切换失败，已恢复：{snapshot.GroupName}";
                else
                    CalibrationStatus = $"标定不可用：{result.ErrorMessage}";
                return true;
            }
        }

        private SpectrumCalibrationSnapshot? GetLoadedCalibrationSnapshot()
        {
            lock (calibrationStateLock)
                return loadedCalibration;
        }

        private SpectrumCalibrationApplyResult CompleteCalibrationLoadWithinDeviceGate(
            int requestVersion,
            SpectrumCalibrationSnapshot? effectiveSnapshot,
            SpectrumCalibrationApplyResult result)
        {
            if (CommitCalibrationResult(requestVersion, effectiveSnapshot, result))
                return result;

            SpectrumCalibrationSnapshot? declaredSnapshot = GetLoadedCalibrationSnapshot();
            if (declaredSnapshot != null && declaredSnapshot != effectiveSnapshot)
            {
                SpectrumCalibrationApplyResult restoreResult;
                try
                {
                    restoreResult = LoadCalibrationFilesCore(declaredSnapshot);
                }
                catch (Exception ex)
                {
                    log.Error("恢复被替代请求之前的标定时发生异常", ex);
                    restoreResult = new SpectrumCalibrationApplyResult(false, ex.GetBaseException().Message);
                }

                if (!restoreResult.IsSuccess)
                {
                    InvalidateLoadedCalibration(
                        declaredSnapshot,
                        $"被替代的标定请求已改变硬件，恢复失败：{restoreResult.ErrorMessage}");
                }
            }

            return new SpectrumCalibrationApplyResult(false, "标定加载请求已被更新的选择替代");
        }

        private void InvalidateLoadedCalibration(SpectrumCalibrationSnapshot expectedSnapshot, string errorMessage)
        {
            lock (calibrationStateLock)
            {
                if (loadedCalibration != expectedSnapshot)
                    return;

                loadedCalibration = null;
                LastOperationError = errorMessage;
                if (calibrationLoadInProgress)
                    OnPropertyChanged(nameof(IsCalibrationReady));
                else
                    CalibrationStatus = $"标定不可用：{errorMessage}";
            }
        }

        internal bool CommitCalibrationConfiguration(CalibrationGroupConfig configuration, int expectedRequestVersion = 0)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            lock (calibrationStateLock)
            {
                if (expectedRequestVersion != 0 && expectedRequestVersion != calibrationRequestVersion)
                {
                    LastOperationError = "标定加载请求已被更新的选择替代";
                    return false;
                }

                CalibrationGroupConfig committedConfiguration = configuration.Clone();
                CalibrationGroup? activeGroup = committedConfiguration.ActiveGroup;
                if (activeGroup == null)
                    throw new InvalidOperationException("标定配置中没有活动分组");

                if (string.IsNullOrEmpty(SerialNumber))
                {
                    LastOperationError = "设备序列号未知，无法保存标定配置";
                    return false;
                }
                if (!committedConfiguration.TrySave(SerialNumber, out string saveError))
                {
                    LastOperationError = $"保存标定配置失败：{saveError}";
                    return false;
                }

                CalibrationGroupConfig = committedConfiguration;
                activeGroup = CalibrationGroupConfig.ActiveGroup
                    ?? throw new InvalidOperationException("标定配置中没有活动分组");
                _WavelengthFile = activeGroup.WavelengthFile;
                _MaguideFile = activeGroup.MaguideFile;
                pendingCalibrationConfigurationVersion = 0;
                OnPropertyChanged(nameof(IsCalibrationConfigurationPending));
                OnPropertyChanged(nameof(ActiveCalibrationGroupName));
                OnPropertyChanged(nameof(WavelengthFile));
                OnPropertyChanged(nameof(MaguideFile));
                OnPropertyChanged(nameof(IsCalibrationReady));

                if (IsCalibrationReady)
                    CalibrationStatus = $"标定已加载：{activeGroup.GroupName}";
                else if (!IsConnected)
                    CalibrationStatus = "连接光谱仪后加载标定文件";

                LastOperationError = string.Empty;
                return true;
            }
        }

        internal static bool TryCreateCalibrationSnapshot(
            string groupName,
            string wavelengthFile,
            string magnitudeFile,
            out SpectrumCalibrationSnapshot? snapshot,
            out string errorMessage)
        {
            snapshot = null;
            errorMessage = string.Empty;
            try
            {
                string wavelengthPath = Path.GetFullPath(wavelengthFile);
                CalibrationFileValidationResult wavelengthValidation = CalibrationFileValidator.ValidateWavelengthFile(wavelengthPath);
                if (!wavelengthValidation.IsValid)
                {
                    errorMessage = $"波长文件无效：{wavelengthValidation.Message}";
                    return false;
                }

                string magnitudePath = Path.GetFullPath(magnitudeFile);
                CalibrationFileValidationResult magnitudeValidation = CalibrationFileValidator.ValidateMaguideFile(magnitudePath);
                if (!magnitudeValidation.IsValid)
                {
                    errorMessage = $"幅值文件无效：{magnitudeValidation.Message}";
                    return false;
                }

                snapshot = new SpectrumCalibrationSnapshot(
                    groupName,
                    wavelengthPath,
                    ComputeFileSha256(wavelengthPath),
                    magnitudePath,
                    ComputeFileSha256(magnitudePath));
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                errorMessage = $"无法读取标定文件：{ex.GetBaseException().Message}";
                return false;
            }
        }

        private static string ComputeFileSha256(string filePath)
        {
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        /// <summary>
        /// Called when the CFW (filter wheel) switches to a position. If a calibration group
        /// matches the ND position name, it auto-switches.
        /// </summary>
        public void OnNDPositionChanged(string ndPositionName)
        {
            string? groupName;
            lock (calibrationStateLock)
                groupName = CalibrationGroupConfig.FindGroupForNDPosition(ndPositionName)?.GroupName;

            if (groupName != null)
            {
                log.Debug($"ND 位置切换至 '{ndPositionName}'，自动切换校准组 '{groupName}'");
                ActiveCalibrationGroupName = groupName;
            }
            else
            {
                log.Debug($"ND 位置切换至 '{ndPositionName}'，无匹配校准组");
            }
        }

        /// <summary>
        /// Called when the filter wheel controller changes position.
        /// Auto-switches calibration group if a mapping exists (by FilterWheelPosition or by ND name).
        /// </summary>
        private void OnFilterWheelPositionChanged(int position)
        {
            // First try to find a group by FilterWheelPosition
            string? groupName;
            string? ndName;
            lock (calibrationStateLock)
            {
                groupName = CalibrationGroupConfig.FindGroupForFilterWheelPosition(position)?.GroupName;
                ndName = groupName == null ? FilterWheelConfig.GetHoleName(position) : null;
            }

            if (groupName != null)
            {
                log.Debug($"滤光轮位置切换至 {position}，自动切换校准组 '{groupName}'");
                ActiveCalibrationGroupName = groupName;
                return;
            }

            // Fallback: try to find by ND name
            if (!string.IsNullOrEmpty(ndName))
            {
                OnNDPositionChanged(ndName);
            }
            else
            {
                log.Debug($"滤光轮位置切换至 {position}，无匹配校准组");
            }
        }

        public SpectrometerManager()
        {
            MaguideFile = "Magiude.dat";
            ApplyActiveGroupCommand = new RelayCommand(a => ApplyActiveGroup());

            // Subscribe to filter wheel position changes for auto-switching calibration groups
            FilterWheelController.PositionChanged += OnFilterWheelPositionChanged;
        }
        public FilterWheelConfig FilterWheelConfig => Config.FilterWheelConfig;

        public async Task<(int Result, string Json)> GetSpectrometerSerialNumbersAsync(CancellationToken cancellationToken = default)
        {
            (int Result, string Json) discovery = await RunExclusiveAsync(token => Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                const int bufferLength = 1024;
                StringBuilder result = new(bufferLength);
                int nativeResult = Spectrometer.CM_Emission_GetAllSN((int)Config.SpectrometerType, GetDiscoveryComPort(), result, bufferLength);
                return (nativeResult, result.ToString());
            }, token), cancellationToken).ConfigureAwait(false);

            if (discovery.Result != 1)
                log.Warn($"获取光谱仪设备列表失败: Type={Config.SpectrometerType}, NativeResult={discovery.Result}");
            return discovery;
        }


        public class SpectrometerSnResult
        {
            [JsonProperty("number")]
            public int Number { get; set; }

            [JsonProperty("ID")]
            public List<string> IDs { get; set; }
        }
        /// <summary>
        /// 将CM_Emission_GetAllSN返回的JSON格式化为用户友好的显示文本
        /// </summary>
        internal static string FormatSerialNumberResult(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "未检测到设备 (返回为空)";

            try
            {
                // 使用强类型反序列化（内部基于反射），直接将 JSON 映射到对象
                var result = JsonConvert.DeserializeObject<SpectrometerSnResult>(raw);

                // 如果解析出来的对象为空，或者包含的ID列表为空
                if (result == null || result.IDs == null || result.IDs.Count == 0)
                {
                    return "未检测到设备";
                }

                // 只有1台设备
                if (result.IDs.Count == 1)
                {
                    return $"设备序列号: {result.IDs[0]}";
                }

                // 多台设备
                var formattedList = result.IDs.Select((sn, idx) => $"  {idx + 1}. {sn}");
                return $"检测到 {result.Number} 台设备:\n" + string.Join("\n", formattedList);
            }
            catch (JsonException)
            {
                // 如果 C++ 那边发生了异常或者返回了非标准 JSON（比如报错信息），直接显示原始内容
                return $"解析失败，原始内容: {raw}";
            }
            catch (Exception ex)
            {
                return $"发生未知错误: {ex.Message}\n原始内容: {raw}";
            }
        }


        public MeasurementDataConfig MeasurementDataConfig { get; set; } = new MeasurementDataConfig();
        public IntTimeConfig IntTimeConfig => MeasurementDataConfig.IntTimeConfig;

        public GetDataConfig GetDataConfig => MeasurementDataConfig.GetDataConfig;



        /// <summary>
        /// 连续测试时间
        /// </summary>
        public int MeasurementInterval { get => _MeasurementInterval; set { if (value <= 0) return;  _MeasurementInterval = value;  OnPropertyChanged(); } }
        private int _MeasurementInterval = 30;
        /// <summary>
        /// 连续测试次数
        /// </summary>
        public int MeasurementNum { get => _MeasurementNum; set { if (value <= 0) return; _MeasurementNum = value; OnPropertyChanged(); } }
        private int _MeasurementNum = 30;
        /// <summary>
        /// 当前测试数
        /// </summary>
        public int LoopMeasureNum { get => _LoopMeasureNum; set { _LoopMeasureNum = value; OnPropertyChanged(); } }
        private int _LoopMeasureNum;


        private static int MyCallback(IntPtr strText, int nLen)
        {
            string text = Marshal.PtrToStringAnsi(strText, nLen);
            log.Debug("光谱仪回调: " + text);
            return 0;
        }

        /// <summary>
        /// Runs one device operation after all earlier operations have completed.
        /// </summary>
        public T RunExclusive<T>(Func<T> operation, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            deviceOperationGate.Wait(cancellationToken);
            try
            {
                return operation();
            }
            finally
            {
                deviceOperationGate.Release();
            }
        }

        /// <summary>
        /// Runs one asynchronous device operation after all earlier operations have completed.
        /// </summary>
        public async Task<T> RunExclusiveAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            await deviceOperationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                deviceOperationGate.Release();
            }
        }

        /// <summary>
        /// Attempts to start an operation immediately instead of queuing another measurement.
        /// </summary>
        public async Task<(bool Entered, T? Result)> TryRunExclusiveAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            if (!await deviceOperationGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                return (false, default);
            }

            try
            {
                return (true, await operation(cancellationToken).ConfigureAwait(false));
            }
            finally
            {
                deviceOperationGate.Release();
            }
        }

        public int Connect(CancellationToken cancellationToken = default) => RunExclusive(ConnectCore, cancellationToken);

        public Task<int> ConnectAsync(CancellationToken cancellationToken = default) =>
            Task.Run(() => Connect(cancellationToken), cancellationToken);

        private int ConnectCore()
        {
            if (IsConnected && Handle != IntPtr.Zero)
            {
                return 1;
            }

            if (Handle != IntPtr.Zero)
            {
                DisconnectCore();
            }

            if (!SpectrometerNativeSession.TryAcquire(SpectrometerNativeSessionOwner.Main))
            {
                log.Warn("光谱仪驱动已被其他会话占用，或上次释放失败，主光谱仪暂时不能连接");
                return OperationBusy;
            }

            try
            {
                LicenseSync.EnsureLicensesSynchronized();
                Handle = Spectrometer.CM_CreateEmission((int)Config.SpectrometerType, MyCallback);
                if (Handle == IntPtr.Zero)
                {
                    ResetConnectionState();
                    log.Error("创建光谱仪实例失败");
                    return -1;
                }

                int result = Spectrometer.CM_Emission_Init(Handle, GetConfiguredComPort(), Config.BaudRate);
                if (result != 1)
                {
                    string errorMessage = Spectrometer.GetErrorMessage(result);
                    log.Error($"光谱仪连接失败: {errorMessage}");
                    DisconnectCore();
                    return result;
                }

                ReadSerialNumberCore();

                // Loading the group updates both file paths. IsConnected remains false here,
                // so ApplyActiveGroup does not load the same files a second time.
                LoadCalibrationConfig();
                HardwareModel = Config.SpectrometerType switch
                {
                    SpectrometerType.CMvSpectra => "SP-100",
                    SpectrometerType.LightModule => "SP-10",
                    SpectrometerType.Gaolitong => "高利通",
                    _ => Config.SpectrometerType.ToString()
                };
                IsConnected = true;

                if (!TryCreateCalibrationSnapshot(ActiveCalibrationGroupName, WavelengthFile, MaguideFile, out SpectrumCalibrationSnapshot? calibration, out string calibrationError))
                {
                    LastOperationError = calibrationError;
                    CalibrationStatus = $"标定不可用：{calibrationError}";
                    log.Error(CalibrationStatus);
                }
                else
                {
                    SpectrumCalibrationApplyResult calibrationResult = LoadCalibrationFilesCore(calibration!);
                    if (!calibrationResult.IsSuccess)
                    {
                        LastOperationError = calibrationResult.ErrorMessage;
                        CalibrationStatus = $"标定不可用：{calibrationResult.ErrorMessage}";
                        log.Error(CalibrationStatus);
                    }
                    else
                    {
                        loadedCalibration = calibration;
                        LastOperationError = string.Empty;
                        CalibrationStatus = $"标定已加载：{calibration!.GroupName}";
                    }
                }

                int sp100Result = ApplySp100ConfigurationCore();
                if (sp100Result != 1)
                {
                    log.Warn($"SP100 参数设置失败: {Spectrometer.GetErrorMessage(sp100Result)}");
                }

                log.Info($"光谱仪连接成功，型号 {HardwareModel}，序列号 {SerialNumber}");
                return 1;
            }
            catch
            {
                DisconnectCore();
                throw;
            }
        }

        public Task<bool> ReconnectAsync(int maxAttempts = 6, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);

            return RunExclusiveAsync(async token =>
            {
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    log.Warn($"尝试重连光谱仪 ({attempt}/{maxAttempts})");
                    DisconnectCore();
                    await Task.Delay(200, token).ConfigureAwait(false);

                    int result = ConnectCore();
                    if (result == 1)
                    {
                        log.Info("光谱仪重连成功");
                        return true;
                    }

                    log.Debug($"重连尝试 {attempt} 失败: {GetOperationErrorMessage(result)}");
                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(200, token).ConfigureAwait(false);
                    }
                }

                return false;
            }, cancellationToken);
        }

        public int Disconnect(CancellationToken cancellationToken = default) => RunExclusive(DisconnectCore, cancellationToken);

        public Task<int> DisconnectAsync(CancellationToken cancellationToken = default) =>
            Task.Run(() => Disconnect(cancellationToken), cancellationToken);

        private int DisconnectCore()
        {
            IntPtr handle = Handle;
            if (handle == IntPtr.Zero)
            {
                ResetConnectionState();
                return 1;
            }

            int closeResult = -1;
            int releaseResult = -1;
            try
            {
                closeResult = Spectrometer.CM_Emission_Close(handle);
            }
            finally
            {
                try
                {
                    releaseResult = Spectrometer.CM_ReleaseEmission(handle);
                }
                finally
                {
                    if (releaseResult != 1)
                        SpectrometerNativeSession.Quarantine(SpectrometerNativeSessionOwner.Main);
                    ResetConnectionState();
                }
            }

            if (closeResult != 1)
            {
                log.Warn($"关闭光谱仪失败: {Spectrometer.GetErrorMessage(closeResult)}");
            }
            if (releaseResult != 1)
            {
                log.Warn($"释放光谱仪失败: {Spectrometer.GetErrorMessage(releaseResult)}");
            }

            return closeResult != 1 ? closeResult : releaseResult;
        }

        private void ResetConnectionState()
        {
            lock (calibrationStateLock)
            {
                calibrationRequestVersion++;
                calibrationLoadInProgress = false;
                pendingCalibrationConfigurationVersion = 0;
                OnPropertyChanged(nameof(IsCalibrationConfigurationPending));
                loadedCalibration = null;
                Handle = IntPtr.Zero;
                IsConnected = false;
                SerialNumber = string.Empty;
                CalibrationStatus = "标定文件尚未加载";
            }
            SpectrometerNativeSession.Release(SpectrometerNativeSessionOwner.Main);
        }

        private int GetConfiguredComPort()
        {
            if (!Config.IsComPort)
            {
                return 0;
            }

            string value = Config.SzComName.Replace("COM", string.Empty, StringComparison.OrdinalIgnoreCase);
            if (int.TryParse(value, out int comPort))
            {
                return comPort;
            }

            throw new FormatException($"无效串口名称: {Config.SzComName}");
        }

        private int GetDiscoveryComPort()
        {
            // 高利通 SDK 只支持通过 USB_GetDeviceList 枚举设备。串口选项只用于连接，
            // 不能传给 GetAllSN，否则原生层会误走串口枚举并返回空列表。
            return Config.SpectrometerType == SpectrometerType.Gaolitong ? 0 : GetConfiguredComPort();
        }

        private void ReadSerialNumberCore()
        {
            try
            {
                StringBuilder serialNumber = new(1024);
                int result = Spectrometer.CM_GetSpectrSerialNumber(Handle, serialNumber);
                if (result == 1 && !string.IsNullOrWhiteSpace(serialNumber.ToString()))
                {
                    SerialNumber = serialNumber.ToString().Trim();
                    return;
                }

                log.Warn($"获取序列号失败: {Spectrometer.GetErrorMessage(result)}");
            }
            catch (Exception ex)
            {
                log.Warn("读取序列号异常", ex);
            }

            SerialNumber = "Unknown";
        }

        private SpectrumCalibrationApplyResult LoadCalibrationFilesCore(SpectrumCalibrationSnapshot snapshot)
        {
            int wavelengthResult = Spectrometer.CM_Emission_LoadWavaLengthFile(Handle, snapshot.WavelengthPath);
            if (wavelengthResult != 1)
            {
                string message = $"加载波长文件失败：{Spectrometer.GetErrorMessage(wavelengthResult)}";
                log.Warn($"{message}，文件: {snapshot.WavelengthPath}");
                return new SpectrumCalibrationApplyResult(false, message);
            }

            log.Info($"加载波长文件成功: {snapshot.WavelengthPath}");
            int magnitudeResult = Spectrometer.CM_Emission_LoadMagiudeFile(Handle, snapshot.MagnitudePath);
            if (magnitudeResult != 1)
            {
                string message = $"加载幅值文件失败：{Spectrometer.GetErrorMessage(magnitudeResult)}";
                log.Warn($"{message}，文件: {snapshot.MagnitudePath}");
                return new SpectrumCalibrationApplyResult(false, message);
            }

            log.Info($"加载幅值文件成功: {snapshot.MagnitudePath}");
            return SpectrumCalibrationApplyResult.Success;
        }

        private int ApplySp100ConfigurationCore()
        {
            if (Handle == IntPtr.Zero)
                return -1;

            log.Debug($"设置 SP100 参数: IsEnabled={SetEmissionSP100Config.IsEnabled}, nStartPos={SetEmissionSP100Config.nStartPos}, nEndPos={SetEmissionSP100Config.nEndPos}, dMeanThreshold={SetEmissionSP100Config.dMeanThreshold}");
            return Spectrometer.CM_SetEmissionSP100(Handle, SetEmissionSP100Config.IsEnabled, SetEmissionSP100Config.nStartPos, SetEmissionSP100Config.nEndPos, SetEmissionSP100Config.dMeanThreshold);
        }

        public Task<int> ApplySp100ConfigurationAsync(CancellationToken cancellationToken = default) =>
            Task.Run(() => RunExclusive(ApplySp100ConfigurationCore, cancellationToken), cancellationToken);

        public string? FindSingleDetectedSerialNumber(CancellationToken cancellationToken = default) =>
            RunExclusive(FindSingleDetectedSerialNumberCore, cancellationToken);

        private string? FindSingleDetectedSerialNumberCore()
        {
            StringBuilder resultJson = new(1024);
            int nativeResult = Spectrometer.CM_Emission_GetAllSN((int)Config.SpectrometerType, GetDiscoveryComPort(), resultJson, resultJson.Capacity);
            if (nativeResult != 1)
            {
                log.Debug($"连接失败后枚举光谱仪失败: Type={Config.SpectrometerType}, NativeResult={nativeResult}");
                return null;
            }

            SpectrometerSnResult? result = JsonConvert.DeserializeObject<SpectrometerSnResult>(resultJson.ToString());
            return result?.IDs?.Count == 1 ? result.IDs[0] : null;
        }

        /// <summary>
        /// 执行校零操作，自动处理快门控制
        /// 可被定时任务和Socket指令共享调用
        /// </summary>
        /// <returns>校零结果：1=成功，其他=失败</returns>
        public async Task<int> PerformDarkCalibrationAsync(
            bool requireShutter = false,
            CancellationToken cancellationToken = default)
        {
            var operation = await TryRunExclusiveAsync(
                token => Task.Run(async () =>
                {
                    token.ThrowIfCancellationRequested();
                    if (!IsConnected || Handle == IntPtr.Zero)
                        return -1;

                    (int result, string error) = await CaptureDarkWithShutterCoreAsync(requireShutter, token).ConfigureAwait(false);
                    LastOperationError = error;
                    return result;
                }, CancellationToken.None),
                cancellationToken).ConfigureAwait(false);

            return operation.Entered ? operation.Result : OperationBusy;
        }

        private async Task<(int Result, string Error)> CaptureDarkWithShutterCoreAsync(
            bool requireShutter,
            CancellationToken cancellationToken)
        {
            bool shouldControlShutter = ShutterController.IsConnected;
            if (requireShutter && !shouldControlShutter)
                return (ShutterOperationFailed, Properties.Resources.NoShutterAutoZero);

            if (shouldControlShutter)
            {
                log.Debug("关闭快门进行校零");
                if (!await ShutterController.CloseShutter().ConfigureAwait(false))
                {
                    string closeError = string.IsNullOrWhiteSpace(ShutterController.LastErrorMessage)
                        ? "快门未能确认关闭"
                        : ShutterController.LastErrorMessage;
                    log.Warn($"{closeError}，校零已取消；尝试恢复打开快门");
                    bool recovered = await ShutterController.OpenShutter().ConfigureAwait(false);
                    string recoveryError = recovered
                        ? string.Empty
                        : string.IsNullOrWhiteSpace(ShutterController.LastErrorMessage)
                            ? "；恢复打开快门也失败，请检查光路"
                            : $"；{ShutterController.LastErrorMessage}，请检查光路";
                    return (ShutterOperationFailed, $"{closeError}，校零已取消{recoveryError}");
                }
            }

            int result;
            bool reopened = true;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                result = Spectrometer.CM_Emission_DarkStorage(Handle, IntTime, Average, 0, fDarkData);
            }
            finally
            {
                if (shouldControlShutter)
                {
                    log.Debug("打开快门");
                    reopened = await ShutterController.OpenShutter().ConfigureAwait(false);
                }
            }

            if (!reopened)
                return (ShutterOperationFailed, string.IsNullOrWhiteSpace(ShutterController.LastErrorMessage)
                    ? "校零后快门未能重新打开，请检查光路"
                    : $"{ShutterController.LastErrorMessage}，请检查光路");
            return result == 1
                ? (1, string.Empty)
                : (result, $"校零失败: {Spectrometer.GetErrorMessage(result)}");
        }

        /// <summary>
        /// Event raised when dark data or light data has been acquired, for chart refresh.
        /// </summary>
        public event EventHandler DataAcquired;

        public async Task<(int CaptureResult, int GenerateResult)> GenerateAmplitudeAsync(
            string outputPath,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

            var result = await RunExclusiveAsync(token => Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                if (!IsConnected || Handle == IntPtr.Zero)
                    return (Capture: -1, Generate: -1);

                int capture = Spectrometer.CM_Emission_DarkStorage(Handle, IntTime, Average, 0, fLightData);
                if (capture != 1)
                    return (Capture: capture, Generate: -1);

                log.Debug($"生成幅值文件参数: IntTime={IntTime}, CSFile={CSFile}, WavelengthFile={WavelengthFile}, MaguideFileOutput={outputPath}");
                int generate = Spectrometer.CM_Emission_CreateMagiude(IntTime, fDarkData, fLightData, CSFile, WavelengthFile, outputPath);
                return (Capture: capture, Generate: generate);
            }, token), cancellationToken).ConfigureAwait(false);

            if (result.Capture != 1)
            {
                string errorMsg = Spectrometer.GetErrorMessage(result.Capture);
                log.Error($"获取 LightData 失败: {errorMsg}");
                return (result.Capture, result.Generate);
            }
            DataAcquired?.Invoke(this, EventArgs.Empty);

            if (result.Generate == 1)
                log.Info($"幅值文件生成成功: {outputPath}");
            else
            {
                string errorMsg = Spectrometer.GetErrorMessage(result.Generate);
                log.Error($"幅值文件生成失败: {errorMsg}");
            }

            return (result.Capture, result.Generate);
        }

        public async Task<int> CaptureLightDataAsync(CancellationToken cancellationToken = default)
        {
            int ret = await CaptureCalibrationDataAsync(fLightData, cancellationToken).ConfigureAwait(false);
            if (ret == 1)
            {
                log.Info("LightData 获取成功");
                DataAcquired?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                string errorMsg = Spectrometer.GetErrorMessage(ret);
                log.Error($"LightData 获取失败: {errorMsg}");
            }
            return ret;
        }

        public async Task<int> CaptureDarkDataAsync(CancellationToken cancellationToken = default)
        {
            int ret = await CaptureCalibrationDataAsync(fDarkData, cancellationToken).ConfigureAwait(false);
            if (ret == 1)
            {
                log.Info("校零成功");
                DataAcquired?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                string errorMsg = Spectrometer.GetErrorMessage(ret);
                log.Error($"校零失败: {errorMsg}");
            }
            return ret;
        }

        private Task<int> CaptureCalibrationDataAsync(float[] destination, CancellationToken cancellationToken)
        {
            return RunExclusiveAsync(token => Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                return IsConnected && Handle != IntPtr.Zero
                    ? Spectrometer.CM_Emission_DarkStorage(Handle, IntTime, Average, 0, destination)
                    : -1;
            }, token), cancellationToken);
        }

        public float[] fDarkData = new float[2048];

        public float[] fLightData = new float[2048];


        public float IntTime { get => _IntTime; set { _IntTime = value; OnPropertyChanged(); } }
        private float _IntTime = 100;

        public int Average { get => _Average; set { _Average = value; OnPropertyChanged(); } }
        private int _Average = 1;

        public string WavelengthFile
        {
            get => _WavelengthFile;
            set
            {
                if (string.Equals(_WavelengthFile, value, StringComparison.Ordinal)) return;
                _WavelengthFile = value;
                OnPropertyChanged();
                OnCalibrationPathChanged();
            }
        }
        private string _WavelengthFile = "WavaLength.dat";

        public string CSFile { get => _CSFile; set { _CSFile = value; OnPropertyChanged(); } }
        private string _CSFile;

        public string MaguideFile
        {
            get => _MaguideFile;
            set
            {
                if (string.Equals(_MaguideFile, value, StringComparison.Ordinal)) return;
                _MaguideFile = value;
                OnPropertyChanged();
                OnCalibrationPathChanged();
            }
        }
        private string _MaguideFile;

        private void OnCalibrationPathChanged()
        {
            OnPropertyChanged(nameof(IsCalibrationReady));
            if (IsConnected && !IsCalibrationReady)
                CalibrationStatus = "标定配置已改变，等待重新加载";
        }

        public string MaguideFileOutput { get => _MaguideFileOutput; set { _MaguideFileOutput = value; OnPropertyChanged(); } }
        private string _MaguideFileOutput;



        public AutodarkParam AutodarkParam { get => _AutodarkParam; set { _AutodarkParam = value; OnPropertyChanged(); } }
        private AutodarkParam _AutodarkParam = new AutodarkParam();

        /// <summary>
        /// 自动校零
        /// </summary>
        public bool EnableAutodark { get => _EnableAutodark; set { _EnableAutodark = value; OnPropertyChanged(); if (value) EnableAdaptiveAutoDark = false;  } }
        private bool _EnableAutodark;

        /// <summary>
        /// 自适应校零
        /// </summary>
        public bool EnableAdaptiveAutoDark { get => _EnableAdaptiveAutoDark; set { _EnableAdaptiveAutoDark = value; OnPropertyChanged();  if (value) EnableAutodark = false;  } }
        private bool _EnableAdaptiveAutoDark;



        /// <summary>
        /// 启动自动积分
        /// </summary>
        public bool EnableAutoIntegration { get => _EnableAutoIntegration; set { _EnableAutoIntegration = value; OnPropertyChanged(); } }
        private bool _EnableAutoIntegration;


        public const int OperationBusy = int.MinValue;

        public async Task<(bool Entered, float? IntegrationTime)> TryGetAutoIntegrationTimeAsync(CancellationToken cancellationToken = default)
        {
            var operation = await TryRunExclusiveAsync(
                token => Task.Run<float?>(() =>
                {
                    token.ThrowIfCancellationRequested();
                    if (!IsConnected || Handle == IntPtr.Zero)
                        return null;

                    (int returnCode, float integrationTime) = GetAutoIntegrationTimeCore();
                    if (returnCode != 1)
                    {
                        log.Warn($"自动积分时间获取失败: {Spectrometer.GetErrorMessage(returnCode)}");
                        return null;
                    }

                    if (GetDataConfig.IsSyncFrequencyEnabled)
                    {
                        COLOR_PARA colorParam = new();
                        float synchronizedTime = integrationTime;
                        int syncResult = Spectrometer.CM_Emission_GetDataSyncfreq(
                            Handle, 0, GetDataConfig.Syncfreq, GetDataConfig.SyncfreqFactor,
                            ref synchronizedTime, Average, GetDataConfig.FilterBW, fDarkData,
                            0, 0, GetDataConfig.SetWL1, GetDataConfig.SetWL2, ref colorParam);
                        if (syncResult == 1)
                            integrationTime = synchronizedTime;
                        else
                            log.Warn($"同步频率调整积分时间失败: {Spectrometer.GetErrorMessage(syncResult)}");
                    }

                    IntTime = integrationTime;
                    return integrationTime;
                }, CancellationToken.None),
                cancellationToken).ConfigureAwait(false);

            return (operation.Entered, operation.Result);
        }

        public async Task<int> PerformAdaptiveDarkCalibrationAsync(CancellationToken cancellationToken = default)
        {
            var operation = await TryRunExclusiveAsync(
                token => Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    if (!IsConnected || Handle == IntPtr.Zero)
                        return -1;
                    return Spectrometer.CM_Emission_Init_Auto_Dark(
                        Handle, AutodarkParam.fTimeStart, AutodarkParam.nStepTime,
                        AutodarkParam.nStepCount, Average);
                }, CancellationToken.None),
                cancellationToken).ConfigureAwait(false);

            return operation.Entered ? operation.Result : OperationBusy;
        }

        public async Task<SpectrumMeasurementResult> MeasureAsync(CancellationToken cancellationToken = default)
        {
            if (!TryStartMeasurement())
                return new SpectrumMeasurementResult(null, ErrorMessage: "光谱测量服务正在停止", IsBusy: true);

            try
            {
                return await MeasureTrackedAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                FinishMeasurement();
            }
        }

        private async Task<SpectrumMeasurementResult> MeasureTrackedAsync(CancellationToken cancellationToken)
        {
            Stopwatch totalStopwatch = Stopwatch.StartNew();
            SpectrumMeasurementProfile profile = new()
            {
                CreateTime = DateTime.Now,
                MeasurementMode = GetDataConfig.IsSyncFrequencyEnabled ? "sync-frequency" : "standard"
            };
            bool operationStarted = false;
            bool profilePersisted = false;

            SpectrumMeasurementResult Failure(int? code, string message)
            {
                profile.ErrorCode = code;
                profile.ErrorMessage = message;
                profile.IsSuccess = false;
                log.Warn(message);
                return new SpectrumMeasurementResult(null, code, message);
            }

            try
            {
                var operation = await TryRunExclusiveAsync(
                    token => Task.Run(async () =>
                    {
                        operationStarted = true;
                        return await CaptureMeasurementCoreAsync(profile, token).ConfigureAwait(false);
                    }, CancellationToken.None),
                    cancellationToken).ConfigureAwait(false);

                if (!operation.Entered)
                    return new SpectrumMeasurementResult(null, ErrorMessage: "光谱仪正在执行其他操作", IsBusy: true);

                MeasurementCapture capture = operation.Result
                    ?? new MeasurementCapture(null, null, null, null, "测量未返回结果");
                if (!capture.ColorParam.HasValue)
                    return Failure(capture.ErrorCode, capture.ErrorMessage ?? "测量失败");

                cancellationToken.ThrowIfCancellationRequested();
                SprectrumModel model = new()
                {
                    ColorParam = capture.ColorParam.Value,
                    TotalDurationMs = totalStopwatch.ElapsedMilliseconds
                };
                totalStopwatch.Stop();
                profile.TotalDurationMs = totalStopwatch.ElapsedMilliseconds;
                ViewResultSpectrum viewResult = ViewResultManager.SaveMeasurement(model, capture.EqeVoltage, capture.EqeCurrent, profile);
                profilePersisted = true;
                return new SpectrumMeasurementResult(viewResult);
            }
            catch (OperationCanceledException)
            {
                profile.ErrorMessage = "测量已取消";
                throw;
            }
            catch (Exception ex)
            {
                log.Error("光谱测量异常", ex);
                return Failure(null, ex.GetBaseException().Message);
            }
            finally
            {
                if (totalStopwatch.IsRunning)
                    totalStopwatch.Stop();
                if (!profilePersisted)
                    profile.TotalDurationMs = totalStopwatch.ElapsedMilliseconds;
                if (operationStarted && !profilePersisted)
                {
                    try
                    {
                        ViewResultManager.SaveMeasurementProfile(profile);
                    }
                    catch (Exception ex)
                    {
                        log.Error("保存测量耗时记录失败", ex);
                    }
                }
                if (operationStarted)
                    log.Info($"测量耗时: total={profile.TotalDurationMs}ms, autoDark={profile.AutoDarkDurationMs ?? 0}ms, autoIntegration={profile.AutoIntegrationDurationMs ?? 0}ms, adaptiveDark={profile.AdaptiveAutoDarkDurationMs ?? 0}ms, acquire={profile.AcquireDurationMs ?? 0}ms, persist={profile.PersistDurationMs ?? 0}ms, success={profile.IsSuccess}, spectrumId={profile.SpectrumId?.ToString() ?? "-"}");
            }
        }

        private async Task<MeasurementCapture> CaptureMeasurementCoreAsync(SpectrumMeasurementProfile profile, CancellationToken cancellationToken)
        {
            MeasurementCapture Failure(int? code, string message)
            {
                profile.ErrorCode = code;
                profile.ErrorMessage = message;
                return new MeasurementCapture(null, null, null, code, message);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!IsConnected || Handle == IntPtr.Zero)
                return Failure(null, "光谱仪未连接");
            if (!TryGetCalibrationNotReadyReason(out string calibrationError))
                return Failure(CalibrationUnavailable, calibrationError);

            profile.InputParametersJson = CreateMeasurementInputSnapshotJson();

            if (EnableAutodark)
            {
                Stopwatch stepStopwatch = Stopwatch.StartNew();
                (int darkResult, string darkError) = await CaptureDarkWithShutterCoreAsync(requireShutter: true, cancellationToken).ConfigureAwait(false);
                profile.AutoDarkDurationMs = stepStopwatch.ElapsedMilliseconds;
                if (darkResult != 1)
                    return Failure(darkResult, darkError);
            }

            float integrationTime = IntTime;
            if (EnableAutoIntegration)
            {
                Stopwatch stepStopwatch = Stopwatch.StartNew();
                (int returnCode, float value) = GetAutoIntegrationTimeCore();
                profile.AutoIntegrationDurationMs = stepStopwatch.ElapsedMilliseconds;
                if (returnCode != 1)
                    return Failure(returnCode, $"自动积分时间获取失败: {Spectrometer.GetErrorMessage(returnCode)}");

                integrationTime = value;
                if (!GetDataConfig.IsSyncFrequencyEnabled)
                    IntTime = integrationTime;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (EnableAdaptiveAutoDark)
            {
                float darkIntegrationTime = EnableAutoIntegration && GetDataConfig.IsSyncFrequencyEnabled ? integrationTime : IntTime;
                Stopwatch stepStopwatch = Stopwatch.StartNew();
                int adaptiveDarkResult = Spectrometer.CM_Emission_AutoDarkStorage(Handle, darkIntegrationTime, Average, 0, fDarkData);
                profile.AdaptiveAutoDarkDurationMs = stepStopwatch.ElapsedMilliseconds;
                if (adaptiveDarkResult == 0)
                    return Failure(adaptiveDarkResult, Properties.Resources.PleaseRunAdaptiveAutoDarkFirst);
                if (adaptiveDarkResult != 1)
                    return Failure(adaptiveDarkResult, $"自适应校零数据获取失败: {Spectrometer.GetErrorMessage(adaptiveDarkResult)}");
            }

            cancellationToken.ThrowIfCancellationRequested();
            COLOR_PARA colorParam = new();
            Stopwatch acquireStopwatch = Stopwatch.StartNew();
            int acquireResult;
            if (GetDataConfig.IsSyncFrequencyEnabled)
            {
                float syncIntegrationTime = EnableAutoIntegration ? integrationTime : IntTime;
                acquireResult = Spectrometer.CM_Emission_GetDataSyncfreq(
                    Handle, 0, GetDataConfig.Syncfreq, GetDataConfig.SyncfreqFactor,
                    ref syncIntegrationTime, Average, GetDataConfig.FilterBW, fDarkData,
                    0, 0, GetDataConfig.SetWL1, GetDataConfig.SetWL2, ref colorParam);
                if (acquireResult == 1 && EnableAutoIntegration)
                    IntTime = syncIntegrationTime;
            }
            else
            {
                acquireResult = Spectrometer.CM_Emission_GetData(
                    Handle, 0, IntTime, Average, GetDataConfig.FilterBW, fDarkData,
                    0, 0, GetDataConfig.SetWL1, GetDataConfig.SetWL2, ref colorParam);
                if (acquireResult == -13007)
                {
                    log.Warn($"采集数据超时，正在重试: {Spectrometer.GetErrorMessage(acquireResult)}");
                    acquireResult = Spectrometer.CM_Emission_GetData(
                        Handle, 0, IntTime, Average, GetDataConfig.FilterBW, fDarkData,
                        0, 0, GetDataConfig.SetWL1, GetDataConfig.SetWL2, ref colorParam);
                }
            }

            profile.AcquireDurationMs = acquireStopwatch.ElapsedMilliseconds;
            if (acquireResult != 1)
                return Failure(acquireResult, $"光谱数据采集失败: {Spectrometer.GetErrorMessage(acquireResult)}");

            colorParam.fPh = colorParam.fPh < 1 ? (float)Math.Round(colorParam.fPh, 4) : (float)Math.Round(colorParam.fPh, 2);

            float? eqeVoltage = null;
            float? eqeCurrent = null;
            if (MainWindowConfig.Instance.EqeEnabled)
            {
                eqeVoltage = MainWindowConfig.Instance.EqeVoltage;
                eqeCurrent = MainWindowConfig.Instance.EqeCurrentMA;
                if (SmuController.IsOpen)
                {
                    (bool entered, SmuMeasurementSnapshot? captured) = await SmuController
                        .TryCaptureMeasurementSnapshotAsync(cancellationToken)
                        .ConfigureAwait(false);
                    if (entered && captured is { } snapshot)
                    {
                        SmuController.ApplyMeasurement(snapshot);
                        eqeVoltage = snapshot.Voltage;
                        eqeCurrent = snapshot.CurrentMA;
                        MainWindowConfig.Instance.EqeVoltage = snapshot.Voltage;
                        MainWindowConfig.Instance.EqeCurrentMA = snapshot.CurrentMA;
                    }
                }
            }

            return new MeasurementCapture(colorParam, eqeVoltage, eqeCurrent, null, null);
        }

        private sealed record MeasurementCapture(
            COLOR_PARA? ColorParam,
            float? EqeVoltage,
            float? EqeCurrent,
            int? ErrorCode,
            string? ErrorMessage);

        private (int ReturnCode, float Value) GetAutoIntegrationTimeCore()
        {
            float integrationTime = 0;
            int result = IntTimeConfig.IsOldVersion
                ? Spectrometer.CM_Emission_GetAutoTime(Handle, ref integrationTime, IntTimeConfig.IntLimitTime, IntTimeConfig.AutoIntTimeB, (int)IntTimeConfig.MaxPercent)
                : Spectrometer.CM_Emission_GetAutoTimeEx(Handle, ref integrationTime, IntTimeConfig.IntLimitTime, IntTimeConfig.AutoIntTimeB, IntTimeConfig.Max, null);
            return (result, integrationTime);
        }

        private bool TryGetCalibrationNotReadyReason(out string errorMessage)
        {
            if (Volatile.Read(ref calibrationLoadInProgress))
            {
                errorMessage = "标定文件正在加载，请稍候";
                return false;
            }
            if (Volatile.Read(ref pendingCalibrationConfigurationVersion) != 0)
            {
                errorMessage = "标定配置正在提交，请稍候";
                return false;
            }

            SpectrumCalibrationSnapshot? snapshot = loadedCalibration;
            if (snapshot == null || !snapshot.MatchesConfigured(ActiveCalibrationGroupName, WavelengthFile, MaguideFile))
            {
                errorMessage = string.IsNullOrWhiteSpace(CalibrationStatus)
                    ? "标定文件尚未成功加载"
                    : CalibrationStatus;
                return false;
            }

            try
            {
                if (!string.Equals(snapshot.WavelengthSha256, ComputeFileSha256(snapshot.WavelengthPath), StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(snapshot.MagnitudeSha256, ComputeFileSha256(snapshot.MagnitudePath), StringComparison.OrdinalIgnoreCase))
                {
                    loadedCalibration = null;
                    CalibrationStatus = "标定文件在加载后发生变化，请重新加载";
                    errorMessage = CalibrationStatus;
                    return false;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                loadedCalibration = null;
                CalibrationStatus = $"无法验证已加载的标定文件：{ex.GetBaseException().Message}";
                errorMessage = CalibrationStatus;
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private string CreateMeasurementInputSnapshotJson()
        {
            return JsonConvert.SerializeObject(new
            {
                RequestedIntTime = IntTime,
                Average,
                GetDataConfig.FilterBW,
                EnableAutodark,
                EnableAdaptiveAutoDark,
                EnableAutoIntegration,
                GetDataConfig.IsSyncFrequencyEnabled,
                GetDataConfig.Syncfreq,
                GetDataConfig.SyncfreqFactor,
                GetDataConfig.SetWL1,
                GetDataConfig.SetWL2,
                CalibrationGroup = loadedCalibration?.GroupName,
                WavelengthFile = loadedCalibration?.WavelengthPath,
                WavelengthSha256 = loadedCalibration?.WavelengthSha256,
                MagnitudeFile = loadedCalibration?.MagnitudePath,
                MagnitudeSha256 = loadedCalibration?.MagnitudeSha256
            });
        }
    }
}
