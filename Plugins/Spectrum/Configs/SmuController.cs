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

        private readonly object _deviceLock = new();
        private (double DelayTime, bool Is4Wire, bool IsFront)? _appliedConnectionSettings;
        private (bool IsSourceV, bool IsChannelA)? _appliedDisplaySettings;
        private bool _isBusy;
        private int _deviceId = -1;

        public SmuConfig Config => SmuConfig.Instance;

        [JsonIgnore]
        public SmuDisplayConfig DisplayConfig { get; } = new();

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
                    _appliedConnectionSettings = null;
                    break;
            }
        }

        private void DisplayConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SmuDisplayConfig.IsSourceV):
                    _appliedDisplaySettings = null;
                    OnPropertyChanged(nameof(MeasureValueLabel));
                    OnPropertyChanged(nameof(LimitValueLabel));
                    OnPropertyChanged(nameof(ParameterHint));
                    break;
                case nameof(SmuDisplayConfig.IsChannelA):
                    _appliedDisplaySettings = null;
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

        private (double DelayTime, bool Is4Wire, bool IsFront) GetCurrentConnectionSettings()
        {
            return (Config.DelayTime, Config.Is4Wire, Config.IsFront);
        }

        private (bool IsSourceV, bool IsChannelA) GetCurrentDisplaySettings()
        {
            return (DisplayConfig.IsSourceV, DisplayConfig.IsChannelA);
        }

        private (double MeasureValue, double LimitValue) GetConvertedMeasureArguments()
        {
            return DisplayConfig.IsSourceV
                ? (DisplayConfig.MeasureVal, DisplayConfig.LimitVal / 1000.0)
                : (DisplayConfig.MeasureVal / 1000.0, DisplayConfig.LimitVal);
        }

        private static string BuildPassSxError(string operation, int resultCode, string? details = null)
        {
            string errorMessage = PassSx.FormatErrorMessage(operation, resultCode);
            return string.IsNullOrWhiteSpace(details)
                ? errorMessage
                : $"{errorMessage} | {details}";
        }

        private static string NormalizeIdn(string? rawIdn)
        {
            return string.IsNullOrWhiteSpace(rawIdn)
                ? string.Empty
                : rawIdn.TrimEnd('\0', '\r', '\n', ' ');
        }

        private static string DecodeIdnBuffer(byte[] idBuffer, int strLen)
        {
            int actualLength = Math.Max(0, Math.Min(strLen, idBuffer.Length));
            if (actualLength == 0)
            {
                actualLength = Array.IndexOf(idBuffer, (byte)0);
                if (actualLength < 0)
                {
                    actualLength = idBuffer.Length;
                }
            }

            return NormalizeIdn(Encoding.Default.GetString(idBuffer, 0, actualLength));
        }

        private (bool Success, string ErrorMessage) ApplyConnectionSettingsCore(bool force = false)
        {
            if (!IsOpen)
            {
                return (false, "源表未连接");
            }

            var settings = GetCurrentConnectionSettings();
            if (!force && _appliedConnectionSettings.HasValue && _appliedConnectionSettings.Value.Equals(settings))
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug($"SMU ApplyConnectionSettings skipped (cached): DevID={_deviceId}, Delay={settings.DelayTime}, Is4Wire={settings.Is4Wire}, IsFront={settings.IsFront}");
                }
                return (true, string.Empty);
            }

            List<string> errors = new();

            if (settings.DelayTime > 0)
            {
                int delayTimeCode = PassSx.SetDelayTimeCode(_deviceId, settings.DelayTime);
                if (!PassSx.IsSuccess(delayTimeCode))
                {
                    string errorMessage = BuildPassSxError("SMU SetDelayTime", delayTimeCode, $"Delay={settings.DelayTime}");
                    log.Warn(errorMessage);
                    errors.Add(errorMessage);
                }
                else if (log.IsDebugEnabled)
                {
                    log.Debug($"SMU SetDelayTime ok: DevID={_deviceId}, Ret={delayTimeCode}, Delay={settings.DelayTime}");
                }
            }

            int set4WireFrontCode = PassSx.Set4WireFrontCode(_deviceId, settings.Is4Wire, settings.IsFront);
            if (!PassSx.IsSuccess(set4WireFrontCode))
            {
                string errorMessage = BuildPassSxError("SMU Set4WireFront", set4WireFrontCode, $"Is4Wire={settings.Is4Wire}, IsFront={settings.IsFront}");
                log.Warn(errorMessage);
                errors.Add(errorMessage);
            }
            else if (log.IsDebugEnabled)
            {
                log.Debug($"SMU Set4WireFront ok: DevID={_deviceId}, Ret={set4WireFrontCode}, Is4Wire={settings.Is4Wire}, IsFront={settings.IsFront}");
            }

            if (errors.Count == 0)
            {
                _appliedConnectionSettings = settings;
            }

            return (errors.Count == 0, string.Join("; ", errors));
        }

        private (bool Success, string ErrorMessage) ApplyDisplaySettingsCore(bool force = false)
        {
            if (!IsOpen)
            {
                return (false, "源表未连接");
            }

            var settings = GetCurrentDisplaySettings();
            if (!force && _appliedDisplaySettings.HasValue && _appliedDisplaySettings.Value.Equals(settings))
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug($"SMU ApplyDisplaySettings skipped (cached): DevID={_deviceId}, IsSourceV={settings.IsSourceV}, IsChannelA={settings.IsChannelA}");
                }
                return (true, string.Empty);
            }

            List<string> errors = new();

            int setSourceVCode = PassSx.cvPssSxSetSourceVCode(_deviceId, settings.IsSourceV);
            if (!PassSx.IsSuccess(setSourceVCode))
            {
                string errorMessage = BuildPassSxError("SMU SetSourceV", setSourceVCode, $"IsSourceV={settings.IsSourceV}");
                log.Warn(errorMessage);
                errors.Add(errorMessage);
            }
            else if (log.IsDebugEnabled)
            {
                log.Debug($"SMU SetSourceV ok: DevID={_deviceId}, Ret={setSourceVCode}, IsSourceV={settings.IsSourceV}");
            }

            int setSrcAorBCode = PassSx.SetSrcAorBCode(_deviceId, settings.IsChannelA);
            if (!PassSx.IsSuccess(setSrcAorBCode))
            {
                string errorMessage = BuildPassSxError("SMU SetSrcAorB", setSrcAorBCode, $"IsChannelA={settings.IsChannelA}");
                log.Warn(errorMessage);
                errors.Add(errorMessage);
            }
            else if (log.IsDebugEnabled)
            {
                log.Debug($"SMU SetSrcAorB ok: DevID={_deviceId}, Ret={setSrcAorBCode}, IsChannelA={settings.IsChannelA}");
            }

            if (errors.Count == 0)
            {
                _appliedDisplaySettings = settings;
            }

            return (errors.Count == 0, string.Join("; ", errors));
        }

        private static string ReadIdnCore(int deviceId)
        {
            const int bufferSize = 1024;

            int strLen = bufferSize;
            StringBuilder idBuilder = new(bufferSize);
            int stringBuilderResult = PassSx.cvPssSxGetIDNCode(deviceId, idBuilder, ref strLen);
            string builderIdn = NormalizeIdn(idBuilder.ToString());
            if (!string.IsNullOrWhiteSpace(builderIdn))
            {
                if (!PassSx.IsSuccess(stringBuilderResult) && log.IsDebugEnabled)
                {
                    log.Debug($"SMU GetIDN returned non-success code but produced content: DevID={deviceId}, Ret={stringBuilderResult}, StrLen={strLen}, IDN={builderIdn}");
                }

                return builderIdn;
            }

            strLen = bufferSize;
            byte[] idBuffer = new byte[strLen];
            int byteArrayResult = PassSx.cvPssSxGetIDNCode(deviceId, idBuffer, ref strLen);
            string bufferIdn = DecodeIdnBuffer(idBuffer, strLen);
            if (!string.IsNullOrWhiteSpace(bufferIdn))
            {
                if (!PassSx.IsSuccess(byteArrayResult) && log.IsDebugEnabled)
                {
                    log.Debug($"SMU GetIDN byte buffer produced content with non-success code: DevID={deviceId}, Ret={byteArrayResult}, StrLen={strLen}, IDN={bufferIdn}");
                }

                return bufferIdn;
            }

            log.Warn($"{BuildPassSxError("SMU GetIDN", byteArrayResult, $"DevID={deviceId}, StringBuilderRet={stringBuilderResult}, ByteArrayRet={byteArrayResult}")}");
            return string.Empty;
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
                    return (false, string.Empty, BuildPassSxError("源表连接失败", devId, $"DevName={Config.DevName}, IsNet={Config.IsNet}, DevType={Config.DevType}"));
                }

                _deviceId = devId;
                _appliedConnectionSettings = null;
                _appliedDisplaySettings = null;

                var connectionResult = ApplyConnectionSettingsCore(force: true);
                if (!connectionResult.Success)
                {
                    log.Warn($"SMU apply connection settings after open returned false: DevID={devId}, {connectionResult.ErrorMessage}");
                }

                var displayResult = ApplyDisplaySettingsCore(force: true);
                if (!displayResult.Success)
                {
                    log.Warn($"SMU apply display settings after open returned false: DevID={devId}, {displayResult.ErrorMessage}");
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
                int closeOutputCode = PassSx.CvPssSxCloseOutputCode(devId);
                if (!PassSx.IsSuccess(closeOutputCode))
                {
                    log.Warn(BuildPassSxError("SMU close output before close", closeOutputCode, $"DevID={devId}"));
                }

                int closeDeviceCode = PassSx.CloseDeviceCode(devId);
                if (!PassSx.IsSuccess(closeDeviceCode))
                {
                    log.Warn(BuildPassSxError("SMU close device", closeDeviceCode, $"DevID={devId}"));
                }

                _deviceId = -1;
                _appliedConnectionSettings = null;
                _appliedDisplaySettings = null;
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

                var displaySettingsResult = ApplyDisplaySettingsCore(force: true);
                if (!displaySettingsResult.Success)
                {
                    return (false, default, $"{displaySettingsResult.ErrorMessage} | IsSourceV={DisplayConfig.IsSourceV}, IsChannelA={DisplayConfig.IsChannelA}");
                }

                var convertedArgs = GetConvertedMeasureArguments();
                if (log.IsDebugEnabled)
                {
                    log.Debug($"SMU StepMeasureData request: DevID={_deviceId}, IsSourceV={DisplayConfig.IsSourceV}, IsChannelA={DisplayConfig.IsChannelA}, Measure={DisplayConfig.MeasureVal}, Limit={DisplayConfig.LimitVal}, ConvertedMeasure={convertedArgs.MeasureValue}, ConvertedLimit={convertedArgs.LimitValue}");
                }

                double rstV = 0;
                double rstI = 0;
                int measureDataCode = PassSx.cvStepMeasureDataCode(_deviceId, DisplayConfig.MeasureVal, DisplayConfig.LimitVal, ref rstV, ref rstI);
                if (!PassSx.IsSuccess(measureDataCode))
                {
                    return (false, default, BuildPassSxError("源表 StepMeasureData 失败", measureDataCode, $"IsSourceV={DisplayConfig.IsSourceV}, IsChannelA={DisplayConfig.IsChannelA}, Measure={DisplayConfig.MeasureVal}, Limit={DisplayConfig.LimitVal}, ConvertedMeasure={convertedArgs.MeasureValue}, ConvertedLimit={convertedArgs.LimitValue}"));
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
                var connectionResult = ApplyConnectionSettingsCore();
                if (!connectionResult.Success)
                {
                    log.Warn(connectionResult.ErrorMessage);
                }

                var displayResult = ApplyDisplaySettingsCore();
                if (!displayResult.Success)
                {
                    log.Warn(displayResult.ErrorMessage);
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

        public bool CloseOutput()
        {
            if (!IsOpen || IsBusy) return false;

            try
            {
                lock (_deviceLock)
                {
                    int closeOutputCode = PassSx.CvPssSxCloseOutputCode(_deviceId);
                    if (!PassSx.IsSuccess(closeOutputCode))
                    {
                        LastErrorMessage = BuildPassSxError("关闭源表输出失败", closeOutputCode, $"DevID={_deviceId}");
                        StatusText = "关闭输出失败";
                        log.Warn(LastErrorMessage);
                        return false;
                    }
                }

                DisplayConfig.ClearOutput();
                _appliedDisplaySettings = null;
                LastErrorMessage = string.Empty;
                StatusText = "输出已关闭";
                log.Info("SMU output closed");
                return true;
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"关闭源表输出失败: {ex.Message}";
                StatusText = "关闭输出失败";
                log.Warn("SMU close output failed", ex);
                return false;
            }
        }
    }
}
