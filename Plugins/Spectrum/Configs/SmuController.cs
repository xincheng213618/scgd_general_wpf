#pragma warning disable CA1001,CA1822,CA1863 // App-lifetime controller owns the operation gate.
using ColorVision.Common.MVVM;
using cvColorVision;
using log4net;
using Newtonsoft.Json;
using SpectrumResources = Spectrum.Properties.Resources;
using System.ComponentModel;
using System.Text;

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

        private readonly SemaphoreSlim operationGate = new(1, 1);
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
            get => Volatile.Read(ref _isBusy);
            private set
            {
                if (_isBusy == value) return;
                Volatile.Write(ref _isBusy, value);
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
        public string ConnectButtonText => IsOpen ? SpectrumResources.DisconnectSourceMeter : SpectrumResources.ConnectSourceMeter;

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
        private string _statusText = SpectrumResources.未连接;

        [JsonIgnore]
        public string StatusSummary => string.IsNullOrWhiteSpace(Version) ? StatusText : $"{StatusText} | {Version}";

        [JsonIgnore]
        public string LastErrorMessage { get => _lastErrorMessage; private set { _lastErrorMessage = value; OnPropertyChanged(); } }
        private string _lastErrorMessage = string.Empty;

        [JsonIgnore]
        public string MeasureValueLabel => DisplayConfig.IsSourceV ? SpectrumResources.SourceValueVoltage : SpectrumResources.SourceValueCurrentMilliamp;

        [JsonIgnore]
        public string LimitValueLabel => DisplayConfig.IsSourceV ? SpectrumResources.LimitMilliamp : SpectrumResources.LimitVoltage;

        [JsonIgnore]
        public string ParameterHint => DisplayConfig.IsSourceV ? SpectrumResources.VoltageSourceModeHint : SpectrumResources.CurrentSourceModeHint;

        [JsonIgnore]
        public double? V => DisplayConfig.V;

        [JsonIgnore]
        public double? I => DisplayConfig.I;

        public SmuController()
        {
            DisplayConfig.PropertyChanged += DisplayConfig_PropertyChanged;
        }

        private void DisplayConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SmuDisplayConfig.IsSourceV))
            {
                OnPropertyChanged(nameof(MeasureValueLabel));
                OnPropertyChanged(nameof(LimitValueLabel));
                OnPropertyChanged(nameof(ParameterHint));
                return;
            }

            if (e.PropertyName == nameof(SmuDisplayConfig.V))
            {
                OnPropertyChanged(nameof(V));
                return;
            }

            if (e.PropertyName == nameof(SmuDisplayConfig.I)) OnPropertyChanged(nameof(I));
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

        private static string BuildPassSxError(string operation, int resultCode, string? details = null)
        {
            string errorMessage = Spectrometer.GetErrorMessage(resultCode);
            string message = string.IsNullOrWhiteSpace(errorMessage)
                ? $"{operation} 失败，错误码: {resultCode}"
                : $"{operation} 失败: {errorMessage}";
            return string.IsNullOrWhiteSpace(details)
                ? message
                : $"{message} | {details}";
        }

        private static string NormalizeIdn(string? rawIdn) => string.IsNullOrWhiteSpace(rawIdn) ? string.Empty : rawIdn.TrimEnd('\0', '\r', '\n', ' ');

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

        private bool ApplyConnectionSettingsCore()
        {
            if (!IsOpen) return false;

            if (Config.DelayTime > 0)
            {
                int delayTimeCode = PassSx.SetDelayTime(_deviceId, Config.DelayTime);
                if (delayTimeCode != 1)
                {
                    log.Warn(BuildPassSxError("SMU SetDelayTime", delayTimeCode, $"Delay={Config.DelayTime}"));
                    return false;
                }
                else if (log.IsDebugEnabled)
                {
                    log.Debug($"SMU SetDelayTime ok: DevID={_deviceId}, Ret={delayTimeCode}, Delay={Config.DelayTime}");
                }
            }

            int set4WireFrontCode = PassSx.Set4WireFront(_deviceId, Config.Is4Wire, Config.IsFront);
            if (set4WireFrontCode != 1)
            {
                log.Warn(BuildPassSxError("SMU Set4WireFront", set4WireFrontCode, $"Is4Wire={Config.Is4Wire}, IsFront={Config.IsFront}"));
                return false;
            }
            else if (log.IsDebugEnabled)
            {
                log.Debug($"SMU Set4WireFront ok: DevID={_deviceId}, Ret={set4WireFrontCode}, Is4Wire={Config.Is4Wire}, IsFront={Config.IsFront}");
            }

            return true;
        }

        private bool ApplyDisplaySettingsCore()
        {
            if (!IsOpen) return false;

            int setSourceVCode = PassSx.SetSourceV(_deviceId, DisplayConfig.IsSourceV);
            if (setSourceVCode != 1)
            {
                log.Warn(BuildPassSxError("SMU SetSourceV", setSourceVCode, $"IsSourceV={DisplayConfig.IsSourceV}"));
                return false;
            }
            else if (log.IsDebugEnabled)
            {
                log.Debug($"SMU SetSourceV ok: DevID={_deviceId}, Ret={setSourceVCode}, IsSourceV={DisplayConfig.IsSourceV}");
            }

            int setSrcAorBCode = PassSx.SetSrcAorB(_deviceId, DisplayConfig.IsChannelA);
            if (setSrcAorBCode != 1)
            {
                log.Warn(BuildPassSxError("SMU SetSrcAorB", setSrcAorBCode, $"IsChannelA={DisplayConfig.IsChannelA}"));
                return false;
            }
            else if (log.IsDebugEnabled)
            {
                log.Debug($"SMU SetSrcAorB ok: DevID={_deviceId}, Ret={setSrcAorBCode}, IsChannelA={DisplayConfig.IsChannelA}");
            }

            return true;
        }

        private static string ReadIdnCore(int deviceId)
        {
            const int bufferSize = 1024;

            int strLen = bufferSize;
            StringBuilder idBuilder = new(bufferSize);
            int stringBuilderResult = PassSx.GetIDN(deviceId, idBuilder, ref strLen);
            string builderIdn = NormalizeIdn(idBuilder.ToString());
            if (!string.IsNullOrWhiteSpace(builderIdn))
            {
                if (stringBuilderResult != 1 && log.IsDebugEnabled)
                {
                    log.Debug($"SMU GetIDN returned non-success code but produced content: DevID={deviceId}, Ret={stringBuilderResult}, StrLen={strLen}, IDN={builderIdn}");
                }

                return builderIdn;
            }

            strLen = bufferSize;
            byte[] idBuffer = new byte[strLen];
            int byteArrayResult = PassSx.GetIDN(deviceId, idBuffer, ref strLen);
            string bufferIdn = DecodeIdnBuffer(idBuffer, strLen);
            if (!string.IsNullOrWhiteSpace(bufferIdn))
            {
                if (byteArrayResult != 1 && log.IsDebugEnabled)
                {
                    log.Debug($"SMU GetIDN byte buffer produced content with non-success code: DevID={deviceId}, Ret={byteArrayResult}, StrLen={strLen}, IDN={bufferIdn}");
                }

                return bufferIdn;
            }

            log.Warn($"{BuildPassSxError("SMU GetIDN", byteArrayResult, $"DevID={deviceId}, StringBuilderRet={stringBuilderResult}, ByteArrayRet={byteArrayResult}")}");
            return string.Empty;
        }

        private string? OpenCore()
        {
            if (IsOpen)
            {
                return Version;
            }

            int devId = PassSx.OpenNetDevice(Config.IsNet, Config.DevName, Config.DevType);
            if (devId < 0)
            {
                log.Warn(BuildPassSxError("源表连接失败", devId, $"DevName={Config.DevName}, IsNet={Config.IsNet}, DevType={Config.DevType}"));
                return null;
            }

            _deviceId = devId;
            try
            {
                if (!ApplyConnectionSettingsCore()) log.Warn($"SMU apply connection settings after open failed: DevID={devId}");
                if (!ApplyDisplaySettingsCore()) log.Warn($"SMU apply display settings after open failed: DevID={devId}");

                return ReadIdnCore(devId);
            }
            catch
            {
                try
                {
                    CloseCore();
                }
                catch (Exception closeException)
                {
                    log.Warn($"SMU cleanup after open failure failed: DevID={devId}", closeException);
                    _deviceId = -1;
                }
                throw;
            }
        }

        private void CloseCore()
        {
            if (_deviceId < 0)
            {
                return;
            }

            int devId = _deviceId;
            try
            {
                int closeOutputCode = PassSx.CloseOutput(devId);
                if (closeOutputCode != 1) log.Warn(BuildPassSxError("SMU close output before close", closeOutputCode, $"DevID={devId}"));
            }
            finally
            {
                int closeDeviceCode = PassSx.CloseDevice(devId);
                _deviceId = -1;
                if (closeDeviceCode != 1) log.Warn(BuildPassSxError("SMU close device", closeDeviceCode, $"DevID={devId}"));
            }
        }

        private SmuMeasurementSnapshot? CaptureMeasurementCore()
        {
            if (_deviceId < 0)
            {
                return null;
            }

            if (!ApplyDisplaySettingsCore())
            {
                log.Warn($"SMU apply display settings before measure failed: DevID={_deviceId}, IsSourceV={DisplayConfig.IsSourceV}, IsChannelA={DisplayConfig.IsChannelA}");
                return null;
            }

            double convertedMeasure = DisplayConfig.IsSourceV ? DisplayConfig.MeasureVal : DisplayConfig.MeasureVal / 1000.0;
            double convertedLimit = DisplayConfig.IsSourceV ? DisplayConfig.LimitVal / 1000.0 : DisplayConfig.LimitVal;
            if (log.IsDebugEnabled)
            {
                log.Debug($"SMU StepMeasureData request: DevID={_deviceId}, IsSourceV={DisplayConfig.IsSourceV}, IsChannelA={DisplayConfig.IsChannelA}, Measure={DisplayConfig.MeasureVal}, Limit={DisplayConfig.LimitVal}, ConvertedMeasure={convertedMeasure}, ConvertedLimit={convertedLimit}");
            }

            double rstV = 0;
            double rstI = 0;
            int measureDataCode = PassSx.StepMeasureData(_deviceId, convertedMeasure, convertedLimit, ref rstV, ref rstI);
            if (measureDataCode != 1)
            {
                log.Warn(BuildPassSxError("源表 StepMeasureData 失败", measureDataCode, $"IsSourceV={DisplayConfig.IsSourceV}, IsChannelA={DisplayConfig.IsChannelA}, Measure={DisplayConfig.MeasureVal}, Limit={DisplayConfig.LimitVal}, ConvertedMeasure={convertedMeasure}, ConvertedLimit={convertedLimit}"));
                return null;
            }

            return new SmuMeasurementSnapshot((float)rstV, (float)(rstI * 1000.0));
        }

        private bool TryBeginOperation()
        {
            if (!operationGate.Wait(0))
                return false;

            try
            {
                IsBusy = true;
                return true;
            }
            catch
            {
                Volatile.Write(ref _isBusy, false);
                operationGate.Release();
                throw;
            }
        }

        private async ValueTask<bool> TryBeginOperationAsync(CancellationToken cancellationToken = default)
        {
            if (!await operationGate.WaitAsync(0, cancellationToken))
                return false;

            try
            {
                IsBusy = true;
                return true;
            }
            catch
            {
                Volatile.Write(ref _isBusy, false);
                operationGate.Release();
                throw;
            }
        }

        private void EndOperation()
        {
            // Publish the idle state before releasing the gate. Otherwise a new operation
            // could acquire the gate and set IsBusy=true before this operation writes false.
            try
            {
                IsBusy = false;
            }
            finally
            {
                operationGate.Release();
            }
        }

        public async Task<bool> OpenAsync()
        {
            if (!await TryBeginOperationAsync())
                return false;

            try
            {
                if (IsOpen) return true;
                if (string.IsNullOrWhiteSpace(Config.DevName))
                {
                    LastErrorMessage = SpectrumResources.DeviceNameRequired;
                    StatusText = SpectrumResources.未连接;
                    return false;
                }

                StatusText = SpectrumResources.ConnectingSourceMeter;
                LastErrorMessage = string.Empty;
                string? version = await Task.Run(OpenCore);
                if (version is null)
                {
                    Version = string.Empty;
                    LastErrorMessage = SpectrumResources.SourceMeterConnectFailedCheckSettings;
                    StatusText = SpectrumResources.ConnectionFailedTitle;
                    RefreshStateProperties();
                    return false;
                }

                Version = version;
                LastErrorMessage = string.Empty;
                StatusText = SpectrumResources.ConnectedStatus;
                RefreshStateProperties();
                log.Info($"SMU opened: DevID={_deviceId}, Version={Version}");
                return true;
            }
            finally
            {
                EndOperation();
            }
        }

        public bool Open()
        {
            if (!TryBeginOperation()) return false;

            try
            {
                if (IsOpen) return true;
                if (string.IsNullOrWhiteSpace(Config.DevName)) return false;

                string? version = OpenCore();
                if (version is null)
                {
                    LastErrorMessage = SpectrumResources.SourceMeterConnectFailedCheckSettings;
                    StatusText = SpectrumResources.ConnectionFailedTitle;
                    RefreshStateProperties();
                    return false;
                }

                Version = version;
                LastErrorMessage = string.Empty;
                StatusText = SpectrumResources.ConnectedStatus;
                RefreshStateProperties();
                log.Info($"SMU opened: DevID={_deviceId}, Version={Version}");
                return true;
            }
            finally
            {
                EndOperation();
            }
        }

        public async Task CloseAsync()
        {
            if (!await TryBeginOperationAsync()) return;

            try
            {
                if (!IsOpen) return;

                StatusText = SpectrumResources.DisconnectingSourceMeter;
                await Task.Run(CloseCore);
                DisplayConfig.ClearOutput();
                Version = string.Empty;
                LastErrorMessage = string.Empty;
                StatusText = SpectrumResources.未连接;
                RefreshStateProperties();
                log.Info("SMU closed");
            }
            finally
            {
                EndOperation();
            }
        }

        public void Close()
        {
            if (!TryBeginOperation()) return;

            try
            {
                if (!IsOpen) return;

                CloseCore();
                DisplayConfig.ClearOutput();
                Version = string.Empty;
                LastErrorMessage = string.Empty;
                StatusText = SpectrumResources.未连接;
                RefreshStateProperties();
                log.Info("SMU closed");
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>
        /// Attempts an immediate, serialized capture for the spectrometer measurement path.
        /// A null result means that the SMU was unavailable, busy, or the native capture failed.
        /// </summary>
        public SmuMeasurementSnapshot? CaptureMeasurementSnapshot()
        {
            if (!TryBeginOperation()) return null;
            try
            {
                return CaptureMeasurementCore();
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>
        /// Attempts an immediate asynchronous capture without racing SMU open, close, or UI measurement.
        /// </summary>
        public async Task<(bool Entered, SmuMeasurementSnapshot? Snapshot)> TryCaptureMeasurementSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            if (!await TryBeginOperationAsync(cancellationToken))
                return (false, null);

            try
            {
                SmuMeasurementSnapshot? snapshot = await Task.Run(CaptureMeasurementCore, CancellationToken.None);
                return (true, snapshot);
            }
            finally
            {
                EndOperation();
            }
        }

        public void ApplyMeasurement(SmuMeasurementSnapshot snapshot)
        {
            DisplayConfig.V = snapshot.Voltage;
            DisplayConfig.I = snapshot.CurrentMA;
        }

        public async Task<bool> MeasureAndApplyAsync()
        {
            if (!await TryBeginOperationAsync()) return false;

            try
            {
                if (!IsOpen) return false;

                StatusText = SpectrumResources.MeasuringAndReading;
                SmuMeasurementSnapshot? snapshot = await Task.Run(CaptureMeasurementCore);
                if (snapshot is null)
                {
                    LastErrorMessage = SpectrumResources.SourceMeterReadFailed;
                    StatusText = SpectrumResources.ReadFailedTitle;
                    return false;
                }

                ApplyMeasurement(snapshot.Value);
                LastErrorMessage = string.Empty;
                StatusText = string.Format(SpectrumResources.ReadCompletedAt, DateTime.Now.ToString("HH:mm:ss"));
                log.Info($"SMU MeasureData: V={V}, I={I} mA");
                return true;
            }
            finally
            {
                EndOperation();
            }
        }

        public bool MeasureData()
        {
            if (!TryBeginOperation()) return false;

            try
            {
                if (!IsOpen) return false;

                SmuMeasurementSnapshot? snapshot = CaptureMeasurementCore();
                if (snapshot is null)
                {
                    LastErrorMessage = SpectrumResources.SourceMeterReadFailed;
                    StatusText = SpectrumResources.ReadFailedTitle;
                    return false;
                }

                ApplyMeasurement(snapshot.Value);
                LastErrorMessage = string.Empty;
                StatusText = string.Format(SpectrumResources.ReadCompletedAt, DateTime.Now.ToString("HH:mm:ss"));
                log.Info($"SMU MeasureData: V={V}, I={I} mA");
                return true;
            }
            finally
            {
                EndOperation();
            }
        }

        public (float voltage, float currentMA) GetVI() => ((float)(DisplayConfig.V ?? 0), (float)(DisplayConfig.I ?? 0));

        public bool CloseOutput()
        {
            if (!TryBeginOperation()) return false;

            try
            {
                if (!IsOpen) return false;

                int closeOutputCode = PassSx.CloseOutput(_deviceId);
                if (closeOutputCode != 1)
                {
                    log.Warn(BuildPassSxError("关闭源表输出失败", closeOutputCode, $"DevID={_deviceId}"));
                    LastErrorMessage = SpectrumResources.SourceMeterCloseOutputFailed;
                    StatusText = SpectrumResources.CloseOutputFailedTitle;
                    return false;
                }

                DisplayConfig.ClearOutput();
                LastErrorMessage = string.Empty;
                StatusText = SpectrumResources.OutputClosed;
                log.Info("SMU output closed");
                return true;
            }
            finally
            {
                EndOperation();
            }
        }
    }
}
