using ColorVision.Common.MVVM;
using cvColorVision;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;

namespace Spectrum.Configs
{
    public readonly struct SmuMeasurementSnapshot
    {
        public SmuMeasurementSnapshot(float voltage, float currentMA)
        {
            Voltage = voltage;
            CurrentMA = currentMA;
        }

        public float Voltage { get; }

        public float CurrentMA { get; }
    }

    public class SmuController : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SmuController));
        private static readonly IReadOnlyList<Pss_Type> _availableDeviceTypes = new[]
        {
            Pss_Type.Keithley_2400,
            Pss_Type.Keithley_2600,
            Pss_Type.Precise_S100,
            Pss_Type.Vxi11Protocol,
            Pss_Type.VictualPss,
        };

        private readonly object _deviceLock = new();
        private (double DelayTime, bool Is4Wire, bool IsFront, bool IsSourceV, bool IsChannelA)? _appliedSettings;
        private bool _isBusy;
        private int _deviceId = -1;

        public SmuConfig Config => SmuConfig.Instance;

        [JsonIgnore]
        public SmuDisplayConfig DisplayConfig { get; } = new();

        public IReadOnlyList<Pss_Type> AvailableDeviceTypes => _availableDeviceTypes;

        [JsonIgnore]
        public bool IsOpen => _deviceId >= 0;

        [JsonIgnore]
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
                RefreshStateProperties();
            }
        }

        [JsonIgnore]
        public bool CanToggleConnection => !IsBusy;

        [JsonIgnore]
        public bool CanMeasure => IsOpen && !IsBusy;

        [JsonIgnore]
        public bool CanCloseOutput => IsOpen && !IsBusy;

        [JsonIgnore]
        public bool CanEditConnectionSettings => !IsOpen && !IsBusy;

        [JsonIgnore]
        public bool CanEditDisplaySettings => !IsBusy;

        [JsonIgnore]
        public string ConnectButtonText => IsOpen ? "断开源表" : "连接源表";

        [JsonIgnore]
        public string Version
        {
            get => _version;
            private set
            {
                _version = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusSummary));
            }
        }
        private string _version = string.Empty;

        [JsonIgnore]
        public string StatusText
        {
            get => _statusText;
            private set
            {
                _statusText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusSummary));
            }
        }
        private string _statusText = "未连接";

        [JsonIgnore]
        public string StatusSummary => string.IsNullOrWhiteSpace(Version) ? StatusText : $"{StatusText} | {Version}";

        [JsonIgnore]
        public string LastErrorMessage { get => _lastErrorMessage; private set { _lastErrorMessage = value; OnPropertyChanged(); } }
        private string _lastErrorMessage = string.Empty;

        [JsonIgnore]
        public string MeasureValueLabel => DisplayConfig.IsSourceV ? "源值 (V)" : "源值 (mA)";

        [JsonIgnore]
        public string LimitValueLabel => DisplayConfig.IsSourceV ? "限制 (mA)" : "限制 (V)";

        [JsonIgnore]
        public string ParameterHint => DisplayConfig.IsSourceV
            ? "电压源模式: 测量值单位为 V，限制值单位为 mA"
            : "电流源模式: 测量值单位为 mA，限制值单位为 V";

        [JsonIgnore]
        public double? V => DisplayConfig.V;

        [JsonIgnore]
        public double? I => DisplayConfig.I;

        public SmuController()
        {
            Config.PropertyChanged += Config_PropertyChanged;
            DisplayConfig.PropertyChanged += DisplayConfig_PropertyChanged;
        }

        private void Config_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SmuConfig.DelayTime):
                case nameof(SmuConfig.Is4Wire):
                case nameof(SmuConfig.IsFront):
                    _appliedSettings = null;
                    break;
            }
        }

        private void DisplayConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SmuDisplayConfig.IsSourceV):
                    _appliedSettings = null;
                    OnPropertyChanged(nameof(MeasureValueLabel));
                    OnPropertyChanged(nameof(LimitValueLabel));
                    OnPropertyChanged(nameof(ParameterHint));
                    break;
                case nameof(SmuDisplayConfig.IsChannelA):
                    _appliedSettings = null;
                    break;
                case nameof(SmuDisplayConfig.V):
                    OnPropertyChanged(nameof(V));
                    break;
                case nameof(SmuDisplayConfig.I):
                    OnPropertyChanged(nameof(I));
                    break;
            }
        }

        private void RefreshStateProperties()
        {
            OnPropertyChanged(nameof(IsOpen));
            OnPropertyChanged(nameof(CanToggleConnection));
            OnPropertyChanged(nameof(CanMeasure));
            OnPropertyChanged(nameof(CanCloseOutput));
            OnPropertyChanged(nameof(CanEditConnectionSettings));
            OnPropertyChanged(nameof(CanEditDisplaySettings));
            OnPropertyChanged(nameof(ConnectButtonText));
        }

        private (double DelayTime, bool Is4Wire, bool IsFront, bool IsSourceV, bool IsChannelA) GetCurrentSettings()
        {
            return (Config.DelayTime, Config.Is4Wire, Config.IsFront, DisplayConfig.IsSourceV, DisplayConfig.IsChannelA);
        }

        private bool ApplySettingsCore(bool force = false)
        {
            if (!IsOpen)
            {
                return false;
            }

            var settings = GetCurrentSettings();
            if (!force && _appliedSettings.HasValue && _appliedSettings.Value.Equals(settings))
            {
                return true;
            }

            bool success = true;
            if (settings.DelayTime > 0 && !PassSx.SetDelayTime(_deviceId, settings.DelayTime))
            {
                log.Warn($"SMU SetDelayTime failed: Delay={settings.DelayTime}");
                success = false;
            }

            if (!PassSx.Set4WireFront(_deviceId, settings.Is4Wire, settings.IsFront))
            {
                log.Warn($"SMU Set4WireFront failed: Is4Wire={settings.Is4Wire}, IsFront={settings.IsFront}");
                success = false;
            }

            if (!PassSx.cvPssSxSetSourceV(_deviceId, settings.IsSourceV))
            {
                log.Warn($"SMU SetSourceV failed: IsSourceV={settings.IsSourceV}");
                success = false;
            }

            if (!PassSx.SetSrcAorB(_deviceId, settings.IsChannelA))
            {
                log.Warn($"SMU SetSrcAorB failed: IsChannelA={settings.IsChannelA}");
                success = false;
            }

            if (success)
            {
                _appliedSettings = settings;
            }

            return success;
        }

        private string ReadIdnCore(int deviceId)
        {
            int strLen = 1024;
            byte[] idBuffer = new byte[strLen];
            if (!PassSx.cvPssSxGetIDN(deviceId, idBuffer, ref strLen))
            {
                log.Warn($"SMU GetIDN failed: DevID={deviceId}");
                return string.Empty;
            }

            int actualLength = Math.Max(0, Math.Min(strLen, idBuffer.Length));
            return Encoding.Default.GetString(idBuffer, 0, actualLength).TrimEnd('\0', '\r', '\n', ' ');
        }

        private (bool Success, string Version, string ErrorMessage) OpenCore()
        {
            lock (_deviceLock)
            {
                if (IsOpen)
                {
                    return (true, Version, string.Empty);
                }

                int devId = PassSx.OpenNetDevice(Config.IsNet, Config.DevName, Config.DevType);
                if (devId < 0)
                {
                    return (false, string.Empty, $"源表连接失败: {Config.DevName}");
                }

                _deviceId = devId;
                _appliedSettings = null;
                if (!ApplySettingsCore(force: true))
                {
                    log.Warn($"SMU apply settings after open returned false: DevID={devId}");
                }

                return (true, ReadIdnCore(devId), string.Empty);
            }
        }

        private void CloseCore()
        {
            lock (_deviceLock)
            {
                if (_deviceId < 0)
                {
                    return;
                }

                int devId = _deviceId;
                try
                {
                    PassSx.CvPssSxCloseOutput(devId);
                }
                catch (Exception ex)
                {
                    log.Warn($"SMU close output before close failed: DevID={devId}", ex);
                }

                try
                {
                    PassSx.CloseDevice(devId);
                }
                catch (Exception ex)
                {
                    log.Warn($"SMU close device failed: DevID={devId}", ex);
                }

                _deviceId = -1;
                _appliedSettings = null;
            }
        }

        private (bool Success, SmuMeasurementSnapshot Snapshot, string ErrorMessage) CaptureMeasurementCore()
        {
            lock (_deviceLock)
            {
                if (_deviceId < 0)
                {
                    return (false, default, "源表未连接");
                }

                if (!ApplySettingsCore())
                {
                    return (false, default, "源表参数应用失败");
                }

                double rstV = 0;
                double rstI = 0;
                bool ok = PassSx.cvMeasureData(_deviceId, DisplayConfig.MeasureVal, DisplayConfig.LimitVal, ref rstV, ref rstI);
                if (!ok)
                {
                    return (false, default, $"源表读取失败: Measure={DisplayConfig.MeasureVal}, Limit={DisplayConfig.LimitVal}");
                }

                return (true, new SmuMeasurementSnapshot((float)rstV, (float)(rstI * 1000.0)), string.Empty);
            }
        }

        public async Task<bool> OpenAsync()
        {
            if (IsOpen) return true;
            if (IsBusy) return false;
            if (string.IsNullOrWhiteSpace(Config.DevName))
            {
                LastErrorMessage = "设备名称不能为空";
                StatusText = "未连接";
                return false;
            }

            IsBusy = true;
            StatusText = "正在连接源表...";
            LastErrorMessage = string.Empty;
            try
            {
                var result = await Task.Run(OpenCore);
                if (!result.Success)
                {
                    Version = string.Empty;
                    LastErrorMessage = result.ErrorMessage;
                    StatusText = "连接失败";
                    log.Warn(result.ErrorMessage);
                    RefreshStateProperties();
                    return false;
                }

                Version = result.Version;
                LastErrorMessage = string.Empty;
                StatusText = "已连接";
                RefreshStateProperties();
                log.Info($"SMU opened: DevID={_deviceId}, Version={Version}");
                return true;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public bool Open()
        {
            if (IsOpen) return true;
            if (string.IsNullOrWhiteSpace(Config.DevName)) return false;

            var result = OpenCore();
            if (!result.Success)
            {
                LastErrorMessage = result.ErrorMessage;
                StatusText = "连接失败";
                log.Warn(result.ErrorMessage);
                RefreshStateProperties();
                return false;
            }

            Version = result.Version;
            LastErrorMessage = string.Empty;
            StatusText = "已连接";
            RefreshStateProperties();
            log.Info($"SMU opened: DevID={_deviceId}, Version={Version}");
            return true;
        }

        public async Task CloseAsync()
        {
            if (!IsOpen || IsBusy) return;

            IsBusy = true;
            StatusText = "正在断开源表...";
            try
            {
                await Task.Run(CloseCore);
                DisplayConfig.ClearOutput();
                Version = string.Empty;
                LastErrorMessage = string.Empty;
                StatusText = "未连接";
                RefreshStateProperties();
                log.Info("SMU closed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void Close()
        {
            if (!IsOpen) return;

            CloseCore();
            DisplayConfig.ClearOutput();
            Version = string.Empty;
            LastErrorMessage = string.Empty;
            StatusText = "未连接";
            RefreshStateProperties();
            log.Info("SMU closed");
        }

        public void ApplySettings()
        {
            if (!IsOpen) return;

            lock (_deviceLock)
            {
                if (!ApplySettingsCore())
                {
                    log.Warn("SMU ApplySettings returned false");
                }
            }
        }

        public SmuMeasurementSnapshot? CaptureMeasurementSnapshot()
        {
            var result = CaptureMeasurementCore();
            if (!result.Success)
            {
                log.Warn(result.ErrorMessage);
            }
            return result.Success ? result.Snapshot : null;
        }

        public void ApplyMeasurement(SmuMeasurementSnapshot snapshot)
        {
            DisplayConfig.V = snapshot.Voltage;
            DisplayConfig.I = snapshot.CurrentMA;
        }

        public async Task<bool> MeasureAndApplyAsync()
        {
            if (!IsOpen || IsBusy) return false;

            IsBusy = true;
            StatusText = "正在点亮并读取...";
            try
            {
                var result = await Task.Run(CaptureMeasurementCore);
                if (!result.Success)
                {
                    LastErrorMessage = result.ErrorMessage;
                    StatusText = "读取失败";
                    log.Warn(result.ErrorMessage);
                    return false;
                }

                ApplyMeasurement(result.Snapshot);
                LastErrorMessage = string.Empty;
                StatusText = $"已读取 {DateTime.Now:HH:mm:ss}";
                log.Info($"SMU MeasureData: V={V}, I={I} mA");
                return true;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public bool MeasureData()
        {
            if (!IsOpen) return false;

            var result = CaptureMeasurementCore();
            if (!result.Success)
            {
                LastErrorMessage = result.ErrorMessage;
                StatusText = "读取失败";
                log.Warn(result.ErrorMessage);
                return false;
            }

            ApplyMeasurement(result.Snapshot);
            LastErrorMessage = string.Empty;
            StatusText = $"已读取 {DateTime.Now:HH:mm:ss}";
            log.Info($"SMU MeasureData: V={V}, I={I} mA");
            return true;
        }

        public (float voltage, float currentMA) GetVI()
        {
            return ((float)(DisplayConfig.V ?? 0), (float)(DisplayConfig.I ?? 0));
        }

        public void CloseOutput()
        {
            if (!IsOpen || IsBusy) return;

            try
            {
                lock (_deviceLock)
                {
                    PassSx.CvPssSxCloseOutput(_deviceId);
                }

                DisplayConfig.ClearOutput();
                LastErrorMessage = string.Empty;
                StatusText = "输出已关闭";
                log.Info("SMU output closed");
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"关闭源表输出失败: {ex.Message}";
                StatusText = "关闭输出失败";
                log.Warn("SMU close output failed", ex);
            }
        }
    }
}
