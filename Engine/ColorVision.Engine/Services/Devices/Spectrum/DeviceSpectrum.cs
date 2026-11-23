using LocalizedText = global::ColorVision.Engine.EngineLocalization;
#pragma warning disable CA1863,CS8601,CS8604
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services.Logging;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Calibration;
using ColorVision.Engine.Services.Devices.Spectrum.Correction;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Engine.Services.PhySpectrums;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.LogImp;
using ColorVision.UI.Views;
using cvColorVision;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Services.Devices.Spectrum
{
    internal readonly record struct SpectrumElectricalInputs(double Voltage, double CurrentValue, bool? CurrentInAmperes)
    {
        // An untagged legacy value keeps the original interpretation at each consumer.
        internal double NativeCurrentAmps => CurrentInAmperes == false ? CurrentValue / 1000.0 : CurrentValue;
        internal double LegacyRemoteCurrent => CurrentInAmperes == true ? CurrentValue * 1000.0 : CurrentValue;
    }

    public class DisplaySpectrumConfig : IDisplayConfigBase
    {
        [DisplayName("启用本地光谱仪")]
        [Description("默认开启；下次打开时直接连接本机光谱仪；已打开的连接在关闭前保持当前模式。")]
        public bool UseLocalSpectrum
        {
            get => _useLocalSpectrum;
            set
            {
                _useLocalSpectrum = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ManualCurrent));
            }
        }
        private bool _useLocalSpectrum = true;
        /// <summary>
        /// 是否光通量模式
        /// </summary>
        [DisplayName("SpectrumFluxMode")]
        public bool IsLuminousFluxMode { get => _IsLuminousFluxMode; set { if (_IsLuminousFluxMode == value) return; _IsLuminousFluxMode = value; OnPropertyChanged(); IsIsLuminousFluxModeChanged?.Invoke(this, value); } }
        private bool _IsLuminousFluxMode;

        public event EventHandler<bool> IsIsLuminousFluxModeChanged;

        [PropertyVisibility(nameof(IsLuminousFluxMode))]
        [DisplayName("SpectrumFluxDivisor")]
        public double Divisor { get => _Divisor; set { _Divisor = value; OnPropertyChanged(); } }
        private double _Divisor = 1.0;


        [Display(Name = nameof(Properties.Resources.Engine_PG_AutoIntegration), ResourceType = typeof(Properties.Resources))]
        public bool IsAutoIntTime { get => _IsAutoIntTime; set { _IsAutoIntTime = value; OnPropertyChanged(); } }
        private bool _IsAutoIntTime;

        [DisplayName("IsEnableNd")]
        public bool IsWithND { get => _IsWithND; set { _IsWithND = value; OnPropertyChanged(); } }
        private bool _IsWithND;

        [DisplayName("SpectrumAutoDark")]
        public bool IsAutoDark { get => _IsAutoDark; set { if (value) IsShutter = false; _IsAutoDark = value; OnPropertyChanged(); } }
        private bool _IsAutoDark;
        [DisplayName("SpectrumUseShutter")]
        public bool IsShutter { get => _IsShutter; set { if (value) IsAutoDark = false; _IsShutter = value; OnPropertyChanged(); } }
        private bool _IsShutter;


        [DisplayName("SpectrumIntegrationTime")]
        public double IntTime { get => _IntTime; set { _IntTime = value; OnPropertyChanged(); } }
        private double _IntTime = 100;

        [Display(Name = nameof(Properties.Resources.Engine_PG_MaxIntegrationTime), ResourceType = typeof(Properties.Resources))]
        public double MaxIntTime { get => _MaxIntTime; set { _MaxIntTime = value; OnPropertyChanged(); } }
        private double _MaxIntTime = 6000;

        [DisplayName("AverageTimes")]
        public int AveNum { get => _AveNum; set { _AveNum = value; OnPropertyChanged(); } }
        private int _AveNum = 1;

        [Display(Name = nameof(Properties.Resources.Engine_PG_MaxAveragingCount), ResourceType = typeof(Properties.Resources))]
        public int MaxAveNum { get => _MaxAveNum; set { _MaxAveNum = value; OnPropertyChanged(); } }
        private int _MaxAveNum = 10;

        [DisplayName("SpectrumNdPosition")]
        public int PortNum { get => _PortNum; set { _PortNum = value; OnPropertyChanged(); } }
        private int _PortNum = 1;




        private readonly object _electricalInputsLock = new();
        private double _manualVoltage = 5;
        private double _manualCurrent = 1;
        private ManualCurrentInput? _manualCurrentInput;
        private SmuReading? _smuReading;

        [JsonIgnore]
        [Browsable(false)]
        public double V
        {
            get => ReadElectricalInputs().Voltage;
            set => ManualVoltage = value;
        }

        [JsonIgnore]
        [Browsable(false)]
        public double I
        {
            get => ReadElectricalInputs().Current;
            set => SetManualCurrent(value, inAmperes: null);
        }

        [JsonProperty("V")]
        [DisplayName("SpectrumVoltage")]
        public double ManualVoltage
        {
            get { lock (_electricalInputsLock) return _manualVoltage; }
            set
            {
                lock (_electricalInputsLock) { _manualVoltage = value; _smuReading = null; }
                OnPropertyChanged();
                NotifyElectricalInputsChanged();
            }
        }

        [JsonIgnore]
        [DisplayName("SpectrumCurrent")]
        public double ManualCurrent
        {
            get
            {
                lock (_electricalInputsLock)
                {
                    var inputs = ReadManualElectricalInputs();
                    return UseLocalSpectrum ? inputs.NativeCurrentAmps : inputs.LegacyRemoteCurrent;
                }
            }
            set => SetManualCurrent(value, UseLocalSpectrum);
        }

        internal void SetManualCurrent(double value, bool? inAmperes)
        {
            lock (_electricalInputsLock)
            {
                _manualCurrent = value;
                _manualCurrentInput = inAmperes is bool amps ? new(value, amps) : null;
                _smuReading = null;
            }
            OnPropertyChanged(nameof(ManualCurrent));
            NotifyElectricalInputsChanged();
        }

        [JsonProperty("I")]
        private double SerializedManualCurrent
        {
            get { lock (_electricalInputsLock) return _manualCurrentInput?.Value ?? _manualCurrent; }
            set { lock (_electricalInputsLock) _manualCurrent = value; }
        }

        [JsonProperty("ManualCurrentInput", NullValueHandling = NullValueHandling.Ignore)]
        private ManualCurrentInput? SerializedManualCurrentInput
        {
            get { lock (_electricalInputsLock) return _manualCurrentInput; }
            set { lock (_electricalInputsLock) _manualCurrentInput = value; }
        }

        [OnDeserializing]
        private void ResetManualCurrentInput(StreamingContext context)
        {
            lock (_electricalInputsLock)
            {
                // Populate can load a legacy configuration over a tagged value. I and ManualCurrentInput may arrive in either order.
                _manualCurrent = _manualCurrentInput?.Value ?? _manualCurrent;
                _manualCurrentInput = null;
                _smuReading = null;
            }
        }

        [OnDeserialized]
        private void NotifyDeserializedManualCurrent(StreamingContext context)
        {
            OnPropertyChanged(nameof(ManualCurrent));
            NotifyElectricalInputsChanged();
        }

        internal (double Voltage, double Current) ReadElectricalInputs()
        {
            var inputs = ReadEffectiveElectricalInputs();
            return (inputs.Voltage, inputs.CurrentValue);
        }

        internal SpectrumElectricalInputs ReadEffectiveElectricalInputs()
        {
            SmuReading? reading;
            SpectrumElectricalInputs manual;
            lock (_electricalInputsLock)
            {
                reading = _smuReading;
                manual = ReadManualElectricalInputs();
            }
            return reading?.IsCurrent() == true ? reading.Inputs : manual;
        }

        private SpectrumElectricalInputs ReadManualElectricalInputs() =>
            new(_manualVoltage, _manualCurrentInput?.Value ?? _manualCurrent, _manualCurrentInput?.InAmperes);

        internal void ApplySmuReadings(double? voltage, double? currentMilliamps, Func<bool> isCurrent,
            DeviceSMU? sourceDevice = null, SMUChannelType? sourceChannel = null)
        {
            if (!isCurrent()) return;
            lock (_electricalInputsLock)
            {
                if (voltage is double v && currentMilliamps is double i && double.IsFinite(v) && double.IsFinite(i))
                    _smuReading = new(new(v, i, CurrentInAmperes: false), isCurrent,
                        sourceDevice == null ? null : new WeakReference<DeviceSMU>(sourceDevice), sourceChannel);
                else if (_smuReading?.IsFrom(sourceDevice, sourceChannel) == true)
                    _smuReading = null;
                else return;
            }
            NotifyElectricalInputsChanged();
        }

        private void NotifyElectricalInputsChanged()
        {
            OnPropertyChanged(nameof(V));
            OnPropertyChanged(nameof(I));
        }

        private sealed record SmuReading(SpectrumElectricalInputs Inputs, Func<bool> IsCurrent,
            WeakReference<DeviceSMU>? SourceDevice, SMUChannelType? SourceChannel)
        {
            internal bool IsFrom(DeviceSMU? device, SMUChannelType? channel) => device != null && channel.HasValue
                && SourceChannel == channel && SourceDevice?.TryGetTarget(out var target) == true && ReferenceEquals(target, device);
        }

        private sealed record ManualCurrentInput(
            [property: JsonProperty(Required = Required.Always)] double Value,
            [property: JsonProperty(Required = Required.Always)] bool InAmperes);


    }

    public partial class DeviceSpectrum : DeviceService<ConfigSpectrum>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DeviceSpectrum));
        private const double CorrectionSpectrumStart = 380d;
        private const double CorrectionSpectrumEnd = 780d;
        private const double CorrectionSpectrumInterval = 0.1d;
        private const int CorrectionSpectrumPointCount = 4001;
        private const double CorrectionWavelengthTolerance = 1e-6;
        private const int CalibrationRestartDebounceMilliseconds = 1000;
        private const int CalibrationRestartCooldownMilliseconds = 4000;
        internal const string SpectrumDriverToolSha256 = "4B7C58696B7A809525F6ABCEA9B3E9C1BF91518EBDC0D19AF31E219654074342";
        private readonly object calibrationRestartSync = new object();
        private readonly SemaphoreSlim calibrationRestartGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim correctionExecutionGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim correctionMeasurementGate = new SemaphoreSlim(1, 1);
        internal SpectrumCalibrationGroupChangeGuard CalibrationGroupChangeGuard { get; } = new SpectrumCalibrationGroupChangeGuard();
        private CancellationTokenSource? calibrationRestartCts;
        private int spectrumContinuousMeasurementLease;
        private int spectrumContinuousStatusObserved;
        private int spectrumContinuousStopAcknowledged;

        public MQTTSpectrum DService { get; set; }
        private readonly DockViewRegistration _view;
        internal DockViewRegistration ViewRegistration => _view;
        internal ViewSpectrum? ExistingView => _view.Current as ViewSpectrum;
        internal ViewSpectrum ViewShell => (ViewSpectrum)_view.GetOrCreate();
        public ViewSpectrum View
        {
            get
            {
                ViewSpectrum view = ViewShell;
                view.EnsureInitialized();
                return view;
            }
        }
        internal void PresentResult(Func<ViewResultSpectrum> createResult)
        {
            ViewSpectrum? view = CaptureVisibleResultView();
            if (view == null) return;
            void Present()
            {
                if (OwnsResultView(view))
                    view.AddViewResultSpectrum(createResult());
            }
            if (view.Dispatcher.CheckAccess()) Present();
            else view.Dispatcher.BeginInvoke(Present);
        }

        internal Action<ViewResultSpectrum>? CaptureLiveResultPresentation()
        {
            ViewSpectrum? view = CaptureVisibleResultView();
            if (view == null) return null;

            var target = new WeakReference<ViewSpectrum>(view);
            return result =>
            {
                if (target.TryGetTarget(out var currentView) && OwnsResultView(currentView))
                    currentView.AddViewResultSpectrum(result);
            };
        }

        private ViewSpectrum? CaptureVisibleResultView() => ExistingView is { } view && OwnsResultView(view) ? view : null;

        private bool OwnsResultView(ViewSpectrum view) => !localDisposed && _view.IsVisible && ReferenceEquals(ExistingView, view);

        public DisplaySpectrumConfig DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<DisplaySpectrumConfig>(Config.Code);

        internal void ApplySmuReadings(MySqlConnectedEndpoint? endpoint, double? voltage, double? currentMilliamps,
            Func<bool> sourceStillCurrent, DeviceSMU? sourceDevice = null, SMUChannelType? sourceChannel = null)
        {
            var owner = new WeakReference<DeviceSpectrum>(this);
            bool IsCurrent() => owner.TryGetTarget(out var device) && !device.IsDisposed
                && ReferenceEquals(device.ResultDatabaseEndpoint, endpoint)
                && (endpoint?.IsCurrent ?? (MySqlControl.GetInstance().ConnectedEndpoint == null))
                && sourceStillCurrent();

            DisplayConfig.ApplySmuReadings(voltage, currentMilliamps, IsCurrent, sourceDevice, sourceChannel);
        }

        public ObservableCollection<TemplateModel<SpectrumResourceParam>> SpectrumResourceParams { get; set; } = new ObservableCollection<TemplateModel<SpectrumResourceParam>>();

        [CommandDisplay("PhysicalSpectrumManager", Order = -1, CategoryOrder = 0)]
        [Category("DeviceConnection")]
        [Description("SpectrumManagerHint")]
        public RelayCommand OpenPhysicalSpectrumManagerCommand { get; set; }

        [Browsable(false)]
        public RelayCommand RefreshDeviceIdCommand { get; set; }

        [CommandDisplay("SpectrumDriverTool", Order = 3, CategoryOrder = 0)]
        [Category("DeviceConnection")]
        [Description("SpectrumDriverToolHint")]
        public RelayCommand OpenSpectrumDriverToolCommand { get; set; }

        [Browsable(false)]
        public RelayCommand UploadLincenseCommand { get; set; }

        [CommandDisplay("AdaptiveZeroCalibration", Order = 3, CategoryOrder = 1)]
        [Category("CalibrationCorrection")]
        [Description("SpectrumDarkHint")]
        public RelayCommand SelfAdaptionInitDarkCommand { get; set; }

        [CommandDisplay("ApaptivezeroCaliSet", Order = 4, CategoryOrder = 1)]
        [Category("CalibrationCorrection")]
        [Description("SpectrumDarkSettingsHint")]
        public RelayCommand SelfAdaptionInitDarkSettingCommand { get; set; }

        [CommandDisplay("EmissionSP100Set", Order = 5, CategoryOrder = 1)]
        [Category("CalibrationCorrection")]
        [Description("SpectrumSp100Hint")]
        public RelayCommand EmissionSP100SettingCommand { get; set; }

        public event Action SelfAdaptionInitDarkStarted;
        public event Action SelfAdaptionInitDarkCompleted;

        [CommandDisplay("SpectrumSearchDevices", Order = 0, CategoryOrder = 0)]
        [Category("DeviceConnection")]
        [Description("SpectrumSearchHint")]
        public RelayCommand GetSpectrSerialNumberCommand { get; set; }

        [CommandDisplay("SpectrumCalibrationGroups", Order = 0, CategoryOrder = 1)]
        [Category("CalibrationCorrection")]
        [Description("SpectrumCalibrationGroupHint")]
        public RelayCommand OpenCalibrationGroupWindowCommand { get; set; }

        [CommandDisplay("ApplyCalibrationGroup", Order = 1, CategoryOrder = 1)]
        [Category("CalibrationCorrection")]
        [Description("SpectrumApplyGroupHint")]
        public RelayCommand ApplyCalibrationGroupCommand { get; set; }

        [CommandDisplay("SpectrumCorrection", Order = 2, CategoryOrder = 1)]
        [Category("CalibrationCorrection")]
        [Description("SpectrumCorrectionHint")]
        public RelayCommand OpenSpectrumCorrectionCommand { get; set; }

        public DeviceSpectrum(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            LocalSession = new();
            _view = new DockViewRegistration(() => new ViewSpectrum(this, true), Config.Name, DisPlayManager.IsControlVisible(Config.Code, Config.Name))
                { PersistenceKey = Config.Code };
            InitializeLocalSpectrum();
            DService = new MQTTSpectrum(this);
            DService.RefreshBackendStatus();
            Config.SynchronizeActiveCalibrationGroupFiles();

            SpectrumResourceParam.Load(SpectrumResourceParams, SysResourceModel.Id);

            EditCommand = new RelayCommand(a =>
            {
                if (!CanConfigure) return;
                EditSpectrum window = new EditSpectrum(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                if (window.ShowDialog() == true && window.OpeningConfigurationTarget is { } target
                    && TryAdmitConfigurationEdit(target, window.Owner))
                {
                    if (window.Draft.DisplaySettingsChanged) ConfigService.Instance.Save<DisplayConfigManager>();
                    if (window.Draft.ServiceSettingsChanged) TryCommitConfiguration(window.Owner, expectedTarget: target);
                }

            }, a => CanConfigure);

            DisplayLazy = new Lazy<DisplaySpectrum>(() => new DisplaySpectrum(this));

            RefreshDeviceIdCommand = new RelayCommand(a => RefreshDeviceId());
            OpenPhysicalSpectrumManagerCommand = new RelayCommand(_ => new PhySpectrumManagerWindow(Config.SN, int.TryParse(Config.ComPort, out int port) ? port : 0)
                { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            OpenSpectrumDriverToolCommand = new RelayCommand(a => OpenSpectrumDriverTool());
            UploadLincenseCommand = new RelayCommand(a => UploadLincense());

            SelfAdaptionInitDarkCommand = new RelayCommand(a => SelfAdaptionInitDark());
            SelfAdaptionInitDarkSettingCommand = new RelayCommand(a => SelfAdaptionInitDarkSetting());
            EmissionSP100SettingCommand = new RelayCommand(a => EmissionSP100Setting());

            GetSpectrSerialNumberCommand = new RelayCommand(async _ => await GetSpectrSerialNumberAsync(), _ => !IsDiscoveringSpectrometers);
            EditDisplayConfigCommand = new RelayCommand(a => EditDisplayConfig());
            OpenCalibrationGroupWindowCommand = new RelayCommand(a => OpenCalibrationGroupWindow());
            ApplyCalibrationGroupCommand = new RelayCommand(a => ApplyActiveCalibrationGroup(true));
            OpenSpectrumCorrectionCommand = new RelayCommand(async _ => await OpenSpectrumCorrectionAsync());

            OpenSpectrumLogCommand = new RelayCommand(a => OpenSpectrumLog());
            ContextMenu.Items.Add(new MenuItem() { Header = LocalizedText.Get("本地光谱仪"), Command = OpenLocalSpectrumManagerCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.SpectrumLog, Command = OpenSpectrumLogCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.CalibrationGroup, Command = OpenCalibrationGroupWindowCommand });
        }

        public async Task OpenSpectrumCorrectionAsync(CancellationToken cancellationToken = default)
        {
            bool entered;
            try
            {
                entered = await correctionExecutionGate.WaitAsync(0, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!entered)
            {
                ShowCorrectionMessage("光谱校正窗口已经打开。", MessageBoxImage.Information);
                return;
            }

            try
            {
                var host = CreateSpectrumCorrectionHost(CaptureCorrectionMeasurementAsync);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var window = new SpectrumCorrectionWindow(host, cancellationToken)
                    {
                        Owner = Application.Current.GetActiveWindow(),
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    };
                    window.ShowDialog();
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                log.Error("Spectrum correction window failed.", ex);
                ShowCorrectionMessage($"打开光谱校正功能失败：{ex.Message}", MessageBoxImage.Error);
            }
            finally
            {
                correctionExecutionGate.Release();
            }
        }

        private Task<SpectrumMeasurementSnapshot> CaptureCorrectionMeasurementAsync(CancellationToken cancellationToken) =>
            CaptureSingleResultAsync(CreateCorrectionMeasurement, cancellationToken, includeCalibrationSource: true);

        private static SpectrumMeasurementSnapshot CreateCorrectionMeasurement(SpectrumCaptureResult capture)
        {
            SpectumResultEntity entity = capture.Model;
            CorrectionCaptureCalibration source = capture.Calibration!;
            double[] relativeSpectrum = LoadCorrectionRelativeSpectrum(entity, capture.FileRoots);
            (double start, double end, double interval, int pointCount) = ResolveCorrectionWavelengthMetadata(entity, relativeSpectrum.Length);
            double[] croppedSpectrum = relativeSpectrum.Take(pointCount).ToArray();

            if (croppedSpectrum.Any(value => !double.IsFinite(value) || value < 0))
                throw new InvalidOperationException("服务返回的相对光谱包含无效或负数值，不能用于光谱校正。");

            double absoluteScale = entity.fPlambda ?? double.NaN;
            if (!double.IsFinite(absoluteScale) || absoluteScale <= 0)
                throw new InvalidOperationException("服务返回的绝对光谱系数无效，不能用于光谱校正。");

            double photometricValue = entity.DataType
                ? entity.LuminousFlux ?? entity.fPh ?? double.NaN
                : entity.fPh ?? double.NaN;

            DateTime measuredAt = entity.CreateDate == default ? DateTime.Now : entity.CreateDate;
            return new SpectrumMeasurementSnapshot(
                entity.Id,
                entity.DeviceCode ?? capture.DeviceCode,
                source.SerialNumber,
                new DateTimeOffset(measuredAt),
                start,
                end,
                interval,
                croppedSpectrum,
                absoluteScale,
                photometricValue,
                entity.IntTime ?? capture.IntegrationTime,
                entity.iAveNum ?? capture.Average,
                entity.DataType ? "LuminousFlux" : "Luminance",
                source.GroupName,
                source.MagnitudePath,
                source.MagnitudeSha256) { SourceWavelengthPath = source.WavelengthPath };
        }

        public Task<SpectrumColorMeasurement> CaptureColorMeasurementAsync(CancellationToken cancellationToken = default) =>
            CaptureSingleResultAsync(capture => CreateColorMeasurement(capture.Model, capture.FileRoots), cancellationToken);

        public async Task<IReadOnlyList<SpectrumColorMeasurementSummary>> GetRecentColorMeasurementsAsync() =>
            await SpectrumColorMeasurementHistory.Capture(this).ReadRecentAsync();

        public async Task<SpectrumColorMeasurement> LoadColorMeasurementAsync(int resultId) =>
            await SpectrumColorMeasurementHistory.Capture(this).LoadAsync(resultId);

        internal static SpectrumColorMeasurement CreateColorMeasurement(SpectumResultEntity entity, SpectrumMeasurementFileRoots fileRoots)
        {
            if (entity.DataType)
                throw new InvalidOperationException("四色校正需要亮度 Y，不能使用光通量 / EQE 测量结果。");
            double y = entity.fPh ?? double.NaN;
            double cieX = entity.fx ?? double.NaN;
            double cieY = entity.fy ?? double.NaN;
            if (!double.IsFinite(y) || !double.IsFinite(cieX) || !double.IsFinite(cieY))
                throw new InvalidOperationException("光谱服务返回的 Y、CIE x 或 CIE y 无效。");

            double[] values = LoadCorrectionRelativeSpectrum(entity, fileRoots);
            if (values.Any(value => !double.IsFinite(value)))
                throw new InvalidOperationException("光谱服务返回的光谱数据包含非有限数值。");

            double start = entity.fSpect1 is float startValue && float.IsFinite(startValue) ? startValue : 380d;
            double interval = entity.fInterval is float intervalValue && float.IsFinite(intervalValue) && intervalValue > 0
                ? intervalValue
                : values.Length > 1000 ? 0.1d : 1d;
            SpectrumValuePoint[] spectrum = new SpectrumValuePoint[values.Length];
            for (int index = 0; index < values.Length; index++)
                spectrum[index] = new SpectrumValuePoint(start + interval * index, values[index]);

            DateTimeOffset measuredAt = entity.CreateDate == default ? default : new DateTimeOffset(entity.CreateDate);
            return new SpectrumColorMeasurement(entity.Id, measuredAt, y, cieX, cieY, spectrum)
            {
                PeakAd = entity.fIp, IntegrationTime = entity.IntTime,
                NdPort = entity.NDPort, DeviceCode = entity.DeviceCode ?? string.Empty,
            };
        }

        internal const string CorrectionCaptureSourceChangedMessage = "光谱仪或所属结果数据库已更改，请重新选择设备后采集。";

        private sealed record CorrectionCaptureCalibration(string SerialNumber, string GroupName, string WavelengthPath,
            string MagnitudePath, string MagnitudeSha256);

        private sealed record SpectrumCaptureResult(SpectumResultEntity Model, SpectrumMeasurementFileRoots FileRoots,
            string DeviceCode, double IntegrationTime, int Average, CorrectionCaptureCalibration? Calibration);

        private async Task<TResult> CaptureSingleResultAsync<TResult>(Func<SpectrumCaptureResult, TResult> createResult,
            CancellationToken cancellationToken, bool includeCalibrationSource = false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryEnterSpectrumMeasurement(out string rejectionReason)) throw new InvalidOperationException(rejectionReason);

            MsgRecord? msgRecord = null;
            try
            {
                MQTTSpectrum service = DService;
                ConfigSpectrum config = Config;
                DeviceConfigurationTarget configurationTarget = CaptureConfigurationTarget();
                bool local = SpectrumBackend.OpensLocally;
                var endpoint = local ? null : ResultDatabaseEndpoint;
                string deviceCode = config.Code;
                string serialNumber = config.SN;
                string sendTopic = service.SendTopic;
                string subscribeTopic = service.SubscribeTopic;

                bool OwnsCapture() => service.OwnsDevice(this) && IsConfigurationTargetCurrent(configurationTarget)
                    && ReferenceEquals(Config, config) && local == SpectrumBackend.OpensLocally
                    && string.Equals(config.Code, deviceCode, StringComparison.Ordinal)
                    && string.Equals(config.SN, serialNumber, StringComparison.Ordinal)
                    && string.Equals(service.SendTopic, sendTopic, StringComparison.Ordinal)
                    && string.Equals(service.SubscribeTopic, subscribeTopic, StringComparison.Ordinal)
                    && (local || (endpoint?.IsCurrent == true && ReferenceEquals(ResultDatabaseEndpoint, endpoint)));
                void EnsureCurrent()
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!OwnsCapture()) throw new InvalidOperationException(CorrectionCaptureSourceChangedMessage);
                }

                EnsureCurrent();
                DeviceStatusType deviceStatus = service.DeviceStatus;
                if (!IsCorrectionCaptureReadyStatus(deviceStatus))
                    throw new InvalidOperationException(deviceStatus switch
                    {
                        DeviceStatusType.Opening => "光谱仪服务正在连接，请连接完成后再采集。",
                        DeviceStatusType.Closing => "光谱仪服务正在关闭，请稍后重试。",
                        DeviceStatusType.SP_Continuous_Mode => "光谱仪正在连续测量，请先停止连续测量。",
                        DeviceStatusType.Closed or DeviceStatusType.UnInit => "光谱仪尚未连接，请先打开服务连接。",
                        DeviceStatusType.OffLine => "光谱仪服务离线，暂时不能采集。",
                        DeviceStatusType.Unauthorized => "光谱仪未授权，暂时不能采集。",
                        _ => $"光谱仪当前状态为 {deviceStatus}，暂时不能进行单次采集。",
                    });

                config.EnsureCalibrationGroups();
                SpectrumMeasurementFileRoots fileRoots = CaptureSpectrumMeasurementFileRoots();
                CorrectionCaptureCalibration? calibration = includeCalibrationSource && !local ? ReadCorrectionCaptureCalibration() : null;
                var settings = DisplayConfig;
                double integrationTime = settings.IntTime;
                int average = settings.AveNum;
                TimeSpan captureTimeout = CalculateCorrectionCaptureTimeout(integrationTime, config.MaxIntegralTime, average,
                    settings.IsAutoIntTime, settings.IsAutoDark || settings.IsShutter);
                EnsureCurrent();
                if (!IsCorrectionCaptureReadyStatus(service.DeviceStatus))
                    throw new InvalidOperationException("光谱仪状态已发生变化，请等待当前操作完成后重试。");

                msgRecord = service.GetDataForCorrection(this, endpoint, captureTimeout.TotalMilliseconds, OwnsCapture);
                MsgRecordState state;
                try { state = await ScheduledDeviceJobHelper.WaitForTerminalStateAsync(msgRecord, captureTimeout, cancellationToken); }
                catch (TimeoutException) { throw new InvalidOperationException($"光谱测量超时（{captureTimeout.TotalSeconds:0} 秒）。"); }
                EnsureCurrent();
                if (state != MsgRecordState.Success)
                {
                    string? detail = msgRecord.MsgReturn?.Message;
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
                        ? $"光谱测量失败（{state}）。" : $"光谱测量失败：{detail}");
                }

                SpectumResultEntity entity;
                if (local && msgRecord.MsgReturn?.Data is Local.LocalSpectrumCommandResult localResult)
                {
                    entity = localResult.Model;
                    if (includeCalibrationSource) calibration = ReadCorrectionCaptureCalibration(localResult.Capture.NdCalibration);
                }
                else
                {
                    if (endpoint == null) throw new InvalidOperationException("本地光谱仪未返回本次采集结果。");
                    int masterId = GetCorrectionMasterId(msgRecord.MsgReturn);
                    using var db = endpoint.CreateDbClient();
                    EnsureCurrent();
                    entity = await db.Queryable<SpectumResultEntity>().Where(item => item.Id == masterId).FirstAsync()
                        ?? throw new InvalidOperationException($"未找到光谱测量结果（MasterId={masterId}）。");
                    EnsureCurrent();
                }

                if (!string.Equals(entity.DeviceCode, deviceCode, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"光谱结果设备不匹配：期望 {deviceCode}，实际 {entity.DeviceCode ?? "<空>"}。");
                if (fileRoots != CaptureSpectrumMeasurementFileRoots())
                    throw new InvalidOperationException("光谱结果文件位置已更改，请重新采集后再校正。");
                TResult result = createResult(new SpectrumCaptureResult(entity, fileRoots, deviceCode, integrationTime, average, calibration));
                if (!local && calibration != null && calibration != ReadCorrectionCaptureCalibration())
                    throw new InvalidOperationException("光谱标定组或标定文件在采集期间发生变化，请重新采集。");
                EnsureCurrent();
                return result;
            }
            finally
            {
                if (msgRecord == null) ReleaseSpectrumMeasurementLease();
                else ReleaseSpectrumMeasurementWhenTerminal(msgRecord);
            }
        }

        private CorrectionCaptureCalibration ReadCorrectionCaptureCalibration(Local.LocalSpectrumNd.Calibration? actual = null)
        {
            var group = Config.ActiveCalibrationGroup;
            string magnitudePath = actual?.MagnitudeFile ?? ResolveActiveMagnitudeFilePath();
            if (!File.Exists(magnitudePath))
                throw new InvalidOperationException($"当前幅值标定文件不存在：{magnitudePath}");
            string? validationError = ValidateCorrectionMagnitudeFile(magnitudePath);
            if (validationError != null)
                throw new InvalidOperationException($"当前幅值标定文件不能用于校正：{validationError}");
            string wavelengthFile = string.IsNullOrWhiteSpace(Config.WavelengthFile) ? group.WavelengthFile : Config.WavelengthFile;
            string wavelengthPath = actual?.WavelengthFile ?? (SpectrumBackend.OpensLocally
                ? Local.LocalSpectrumSession.ResolvePath(wavelengthFile)
                : ResolveCalibrationFilePath(wavelengthFile, ServiceConfig.Instance.CVMainService_x64));
            return new CorrectionCaptureCalibration(Config.SN ?? string.Empty, actual?.GroupName ?? group.GroupName,
                wavelengthPath, magnitudePath, ComputeFileSha256(magnitudePath));
        }

        internal SpectrumCorrectionHost CreateSpectrumCorrectionHost(Func<CancellationToken, Task<SpectrumMeasurementSnapshot>> capture)
        {
            var target = CaptureConfigurationTarget();
            ConfigSpectrum config = Config;
            MQTTSpectrum service = DService;
            string deviceCode = config.Code;
            string serialNumber = config.SN;
            string sendTopic = config.SendTopic;
            string subscribeTopic = config.SubscribeTopic;
            bool local = SpectrumBackend.OpensLocally;
            SpectrumMeasurementSnapshot? measurement = null;
            string? wavelengthFile = null;

            bool IsCurrent() => IsConfigurationTargetCurrent(target) && ReferenceEquals(Config, config)
                && service.OwnsDevice(this)
                && string.Equals(config.Code, deviceCode, StringComparison.Ordinal)
                && string.Equals(config.SN, serialNumber, StringComparison.OrdinalIgnoreCase)
                && string.Equals(config.SendTopic, sendTopic, StringComparison.Ordinal)
                && string.Equals(config.SubscribeTopic, subscribeTopic, StringComparison.Ordinal)
                && SpectrumBackend.OpensLocally == local;

            async Task<SpectrumMeasurementSnapshot> Capture(CancellationToken token)
            {
                measurement = null;
                if (!IsCurrent()) throw new InvalidOperationException(CorrectionTargetChangedMessage);
                string requestedWavelength = config.WavelengthFile;
                SpectrumMeasurementSnapshot snapshot = await capture(token);
                token.ThrowIfCancellationRequested();
                if (!IsCurrent() || (!local && !string.Equals(config.WavelengthFile, requestedWavelength, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException(CorrectionTargetChangedMessage);

                wavelengthFile = local ? config.WavelengthFile : requestedWavelength;
                measurement = snapshot;
                return snapshot;
            }

            return new SpectrumCorrectionHost(Capture, (request, token) => ApplyCorrectionMagnitudeFileAsync(
                request, target, measurement, wavelengthFile, local, IsCurrent, token));
        }

        private const string CorrectionTargetChangedMessage = "光谱仪或校正配置已变化，请重新打开校正窗口并采集。";

        private async Task<SpectrumCorrectionApplyResult> ApplyCorrectionMagnitudeFileAsync(
            SpectrumCorrectionApplyRequest request, DeviceConfigurationTarget target, SpectrumMeasurementSnapshot? measurement,
            string? wavelengthFile, bool local, Func<bool> isCurrent, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            if (!isCurrent()) return SpectrumCorrectionApplyResult.Failure(CorrectionTargetChangedMessage);
            if (measurement == null || string.IsNullOrWhiteSpace(measurement.MagnitudeFileSha256)
                || !string.Equals(request.ExpectedSourceMagnitudeSha256, measurement.MagnitudeFileSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(request.CalibrationGroupName, measurement.CalibrationGroupName, StringComparison.OrdinalIgnoreCase))
                return SpectrumCorrectionApplyResult.Failure("缺少本次光谱采集或原幅值文件校验记录，请重新采集并生成校正文件。");
            if (!await correctionMeasurementGate.WaitAsync(0, cancellationToken))
                return SpectrumCorrectionApplyResult.Failure("光谱仪正在执行其他测量或校正操作，不能应用幅值标定文件。");

            bool localReserved = false;
            try
            {
                if (!isCurrent()) return SpectrumCorrectionApplyResult.Failure(CorrectionTargetChangedMessage);
                DeviceStatusType deviceStatus = DService.DeviceStatus;
                if (!IsCorrectionCaptureReadyStatus(deviceStatus))
                {
                    return SpectrumCorrectionApplyResult.Failure(deviceStatus == DeviceStatusType.SP_Continuous_Mode
                        ? "光谱仪正在连续测量，请先停止连续测量再应用幅值标定文件。"
                        : $"光谱仪当前状态为 {deviceStatus}，不能安全应用幅值标定文件。");
                }
                if (local)
                {
                    SpectrumBackend.BeginLocalCommand();
                    localReserved = true;
                }

                SpectrumCalibrationGroup? activeGroup = Config.CalibrationGroups.FirstOrDefault(group =>
                    string.Equals(group.GroupName, Config.ActiveCalibrationGroupName, StringComparison.OrdinalIgnoreCase));
                if (activeGroup == null || !string.Equals(activeGroup.GroupName, measurement.CalibrationGroupName, StringComparison.OrdinalIgnoreCase)
                    || !CorrectionWavelengthMatches(Config.WavelengthFile, wavelengthFile, measurement, local))
                    return SpectrumCorrectionApplyResult.Failure("标定组或波长文件已变化，请重新采集后再应用。");

                string currentSourcePath = ResolveActiveMagnitudeFilePath();
                if (!string.Equals(currentSourcePath, measurement.MagnitudeFilePath, StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(currentSourcePath))
                    return SpectrumCorrectionApplyResult.Failure("原幅值标定路径已变化或文件不存在，请重新采集后再应用。");
                if (string.IsNullOrWhiteSpace(request.MagnitudeFilePath) || string.IsNullOrWhiteSpace(request.ExpectedGeneratedMagnitudeSha256))
                    return SpectrumCorrectionApplyResult.Failure("缺少新幅值标定文件或校验值，请重新生成校正文件。");

                string generatedPath = Path.GetFullPath(request.MagnitudeFilePath);
                if (string.Equals(generatedPath, currentSourcePath, StringComparison.OrdinalIgnoreCase))
                    return SpectrumCorrectionApplyResult.Failure("请使用新生成的幅值标定文件。");
                if (!File.Exists(generatedPath))
                    return SpectrumCorrectionApplyResult.Failure($"新幅值标定文件不存在：{generatedPath}");

                using (var source = new FileStream(currentSourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var generated = new FileStream(generatedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (!string.Equals(Convert.ToHexString(SHA256.HashData(source)), measurement.MagnitudeFileSha256, StringComparison.OrdinalIgnoreCase))
                        return SpectrumCorrectionApplyResult.Failure("原幅值标定文件在采集后发生了变化，请重新采集并生成校正文件。");
                    if (!string.Equals(Convert.ToHexString(SHA256.HashData(generated)), request.ExpectedGeneratedMagnitudeSha256, StringComparison.OrdinalIgnoreCase))
                        return SpectrumCorrectionApplyResult.Failure("新幅值标定文件在生成后发生了变化，请重新生成校正文件。");
                    generated.Position = 0;
                    string? validationError = ValidateCorrectionMagnitudeFile(generated);
                    if (validationError != null) return SpectrumCorrectionApplyResult.Failure(validationError);

                    cancellationToken.ThrowIfCancellationRequested();
                    if (!isCurrent()) return SpectrumCorrectionApplyResult.Failure(CorrectionTargetChangedMessage);
                    using var applyScope = CalibrationGroupChangeGuard.EnterApply();
                    bool saved = target.TrySave(this, previous => UpdateStoredCorrectionMagnitudeFile(previous, measurement,
                        wavelengthFile, local, generatedPath));
                    if (!saved || !isCurrent()) return SpectrumCorrectionApplyResult.Failure(CorrectionTargetChangedMessage);

                    activeGroup.MaguideFile = generatedPath;
                    Config.MaguideFile = generatedPath;
                }

                bool restartRequested = false;
                try
                {
                    if (!local && isCurrent()) restartRequested = RestartCalibrationService(target, request: null);
                    if (isCurrent()) NotifyConfigChanged();
                }
                catch (Exception ex)
                {
                    log.Warn("The corrected Spectrum magnitude file was saved, but follow-up failed.", ex);
                }

                if (local) return SpectrumCorrectionApplyResult.Success(generatedPath, "新 DAT 已应用，下次采集时生效。");
                return restartRequested
                    ? SpectrumCorrectionApplyResult.PendingRestart(generatedPath, "新 DAT 已保存并请求服务重启；恢复后请重新采集验证。")
                    : SpectrumCorrectionApplyResult.Success(generatedPath, "新 DAT 已保存，未发出服务重启请求。请重启服务后重新采集验证。");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                log.Error("Failed to apply corrected Spectrum magnitude file.", ex);
                return SpectrumCorrectionApplyResult.Failure($"应用新幅值标定文件失败：{ex.Message}");
            }
            finally
            {
                if (localReserved) SpectrumBackend.EndLocalCommand();
                ReleaseSpectrumMeasurementLease();
            }
        }

        private static string UpdateStoredCorrectionMagnitudeFile(string? previous, SpectrumMeasurementSnapshot measurement,
            string? wavelengthFile, bool local, string generatedPath)
        {
            if (string.IsNullOrWhiteSpace(previous)) throw new InvalidOperationException(CorrectionTargetChangedMessage);
            using var reader = new JsonTextReader(new StringReader(previous)) { DateParseHandling = DateParseHandling.None };
            var json = JObject.Load(reader);
            var stored = json.ToObject<ConfigSpectrum>()!;
            stored.SynchronizeActiveCalibrationGroupFiles();
            string storedPath = local ? Local.LocalSpectrumSession.ResolvePath(stored.MaguideFile)
                : ResolveCalibrationFilePath(stored.MaguideFile, ServiceConfig.Instance.CVMainService_x64);
            if (!string.Equals(stored.SN, measurement.SerialNumber, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(stored.ActiveCalibrationGroupName, measurement.CalibrationGroupName, StringComparison.OrdinalIgnoreCase)
                || !CorrectionWavelengthMatches(stored.WavelengthFile, wavelengthFile, measurement, local)
                || !string.Equals(storedPath, measurement.MagnitudeFilePath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(CorrectionTargetChangedMessage);

            SetMagnitude(json);
            if (json.GetValue(nameof(ConfigSpectrum.CalibrationGroups), StringComparison.OrdinalIgnoreCase) is JArray groups)
            {
                JObject? group = groups.OfType<JObject>().FirstOrDefault(item => string.Equals(
                    (string?)item.GetValue(nameof(SpectrumCalibrationGroup.GroupName), StringComparison.OrdinalIgnoreCase),
                    measurement.CalibrationGroupName, StringComparison.OrdinalIgnoreCase));
                if (group != null) SetMagnitude(group);
            }
            return json.ToString(Formatting.None);

            void SetMagnitude(JObject item)
            {
                var property = item.Property(nameof(ConfigSpectrum.MaguideFile), StringComparison.OrdinalIgnoreCase);
                if (property != null) property.Value = generatedPath;
                else item[nameof(ConfigSpectrum.MaguideFile)] = generatedPath;
            }
        }

        private static bool CorrectionWavelengthMatches(string? wavelengthFile, string? capturedSetting,
            SpectrumMeasurementSnapshot measurement, bool local)
        {
            if (string.IsNullOrWhiteSpace(measurement.SourceWavelengthPath))
                return string.Equals(wavelengthFile, capturedSetting, StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(wavelengthFile)) return false;
            string path = local ? Local.LocalSpectrumSession.ResolvePath(wavelengthFile)
                : ResolveCalibrationFilePath(wavelengthFile, ServiceConfig.Instance.CVMainService_x64);
            return string.Equals(path, measurement.SourceWavelengthPath, StringComparison.OrdinalIgnoreCase);
        }

        internal static TimeSpan CalculateCorrectionCaptureTimeout(
            double integrationTimeMilliseconds,
            double maximumIntegrationTimeMilliseconds,
            int average,
            bool autoIntegration,
            bool includesDarkMeasurement)
        {
            const double minimumTimeoutMilliseconds = 35_000;
            const double maximumTimeoutMilliseconds = 30 * 60 * 1000;
            const double transportAndProcessingMarginMilliseconds = 30_000;

            double currentIntegration = double.IsFinite(integrationTimeMilliseconds) && integrationTimeMilliseconds > 0
                ? integrationTimeMilliseconds
                : 1;
            double maximumIntegration = double.IsFinite(maximumIntegrationTimeMilliseconds) && maximumIntegrationTimeMilliseconds > 0
                ? maximumIntegrationTimeMilliseconds
                : currentIntegration;
            double effectiveIntegration = autoIntegration
                ? Math.Max(currentIntegration, maximumIntegration)
                : currentIntegration;
            int effectiveAverage = Math.Clamp(average, 1, 1000);
            int exposureCycles = includesDarkMeasurement ? 2 : 1;
            double acquisitionMilliseconds = effectiveIntegration * effectiveAverage * exposureCycles;
            double timeoutMilliseconds = double.IsFinite(acquisitionMilliseconds)
                ? acquisitionMilliseconds + transportAndProcessingMarginMilliseconds
                : maximumTimeoutMilliseconds;

            return TimeSpan.FromMilliseconds(Math.Clamp(
                timeoutMilliseconds,
                minimumTimeoutMilliseconds,
                maximumTimeoutMilliseconds));
        }

        internal static bool IsCorrectionCaptureReadyStatus(DeviceStatusType status) =>
            status is DeviceStatusType.Opened or DeviceStatusType.Free or DeviceStatusType.LiveOpened;

        internal bool TryEnterSpectrumMeasurement(out string rejectionReason)
        {
            if (!correctionMeasurementGate.Wait(0))
            {
                rejectionReason = "光谱仪正在执行其他测量或校正操作。";
                return false;
            }

            rejectionReason = string.Empty;
            return true;
        }

        internal bool TryEnterSpectrumContinuousMeasurement(out string rejectionReason)
        {
            if (!TryEnterSpectrumMeasurement(out rejectionReason))
                return false;

            Interlocked.Exchange(ref spectrumContinuousMeasurementLease, 1);
            Interlocked.Exchange(ref spectrumContinuousStatusObserved, 0);
            Interlocked.Exchange(ref spectrumContinuousStopAcknowledged, 0);
            return true;
        }

        internal void ReleaseSpectrumMeasurementWhenTerminal(MsgRecord msgRecord)
        {
            ArgumentNullException.ThrowIfNull(msgRecord);
            int released = 0;
            if (msgRecord.WorkerCompletion != null)
            {
                msgRecord.TerminalStateCommitted += Complete;
                Complete(msgRecord.MsgRecordState);
                return;

                void Complete(MsgRecordState state)
                {
                    if (!TryRelease(state)) return;
                    msgRecord.TerminalStateCommitted -= Complete;
                    ReleaseSpectrumMeasurementLease();
                }
            }

            EventHandler<MsgRecordState>? stateChanged = null;
            stateChanged = (_, state) =>
            {
                if (!TryRelease(state)) return;
                msgRecord.MsgRecordStateChanged -= stateChanged;
                ReleaseSpectrumMeasurementLease();
            };

            msgRecord.MsgRecordStateChanged += stateChanged;
            stateChanged(msgRecord, msgRecord.MsgRecordState);

            bool TryRelease(MsgRecordState state) => MsgRecord.IsTerminal(state) && Interlocked.Exchange(ref released, 1) == 0;
        }

        internal void ReleaseSpectrumContinuousStartWhenTerminal(MsgRecord msgRecord)
        {
            ArgumentNullException.ThrowIfNull(msgRecord);
            EventHandler<MsgRecordState>? stateChanged = null;
            stateChanged = (_, state) =>
            {
                if (state is not (MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout))
                    return;

                msgRecord.MsgRecordStateChanged -= stateChanged;
                if (state is MsgRecordState.Fail or MsgRecordState.Timeout)
                    ReleaseSpectrumContinuousMeasurementLease();
            };

            msgRecord.MsgRecordStateChanged += stateChanged;
            stateChanged(msgRecord, msgRecord.MsgRecordState);
        }

        internal void ReleaseSpectrumContinuousStopWhenTerminal(MsgRecord msgRecord)
        {
            ArgumentNullException.ThrowIfNull(msgRecord);
            EventHandler<MsgRecordState>? stateChanged = null;
            stateChanged = (_, state) =>
            {
                if (state is not (MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout))
                    return;

                msgRecord.MsgRecordStateChanged -= stateChanged;
                if (state == MsgRecordState.Success)
                {
                    Interlocked.Exchange(ref spectrumContinuousStopAcknowledged, 1);
                    if (DService.DeviceStatus != DeviceStatusType.SP_Continuous_Mode)
                        ReleaseSpectrumContinuousMeasurementLease();
                }
            };

            msgRecord.MsgRecordStateChanged += stateChanged;
            stateChanged(msgRecord, msgRecord.MsgRecordState);
        }

        internal void ReleaseSpectrumMeasurementLease() => correctionMeasurementGate.Release();

        internal void ReleaseSpectrumContinuousMeasurementLease()
        {
            if (Interlocked.Exchange(ref spectrumContinuousMeasurementLease, 0) == 1)
                correctionMeasurementGate.Release();
            Interlocked.Exchange(ref spectrumContinuousStatusObserved, 0);
            Interlocked.Exchange(ref spectrumContinuousStopAcknowledged, 0);
        }

        internal void ObserveSpectrumDeviceStatus(DeviceStatusType status)
        {
            if (Volatile.Read(ref spectrumContinuousMeasurementLease) != 1)
                return;

            if (status == DeviceStatusType.SP_Continuous_Mode)
            {
                Interlocked.Exchange(ref spectrumContinuousStatusObserved, 1);
            }
            else if (Volatile.Read(ref spectrumContinuousStatusObserved) == 1 &&
                     Volatile.Read(ref spectrumContinuousStopAcknowledged) == 1)
            {
                ReleaseSpectrumContinuousMeasurementLease();
            }
        }

        private static int GetCorrectionMasterId(MsgReturn? msgReturn)
        {
            if (msgReturn?.Data == null)
                throw new InvalidOperationException("光谱服务未返回测量结果编号。");

            JToken data = msgReturn.Data as JToken ?? JToken.FromObject(msgReturn.Data);
            int? masterId = data["MasterId"]?.Value<int?>() ?? data["masterId"]?.Value<int?>();
            if (masterId is null or <= 0)
                throw new InvalidOperationException("光谱服务返回的测量结果编号无效。");

            return masterId.Value;
        }

        internal SpectrumMeasurementFileRoots CaptureSpectrumMeasurementFileRoots() =>
            new(Config.FileServerCfg?.DataBasePath,
                Path.GetDirectoryName(ConfigService.Instance?.GetRequiredService<ServiceConfig>()?.CVMainService_x64),
                Environment.CurrentDirectory);

        private static double[] LoadCorrectionRelativeSpectrum(SpectumResultEntity entity, SpectrumMeasurementFileRoots fileRoots)
        {
            string? json = entity.fPL;
            if (!string.IsNullOrWhiteSpace(entity.fPL_file_name))
            {
                string spectrumPath = ResolveSpectrumResultFilePath(entity.fPL_file_name, fileRoots);
                if (!File.Exists(spectrumPath))
                    throw new InvalidOperationException($"光谱数据文件不存在：{spectrumPath}");
                json = File.ReadAllText(spectrumPath);
            }

            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("光谱服务未返回相对光谱数据。");

            double[] spectrum = JsonConvert.DeserializeObject<double[]>(json) ?? Array.Empty<double>();
            if (spectrum.Length == 0)
                throw new InvalidOperationException("光谱服务返回的相对光谱数据为空。");
            return spectrum;
        }

        private static string ResolveSpectrumResultFilePath(string path, SpectrumMeasurementFileRoots fileRoots)
        {
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path, fileRoots.WorkingDirectory);

            string? dataBasePath = fileRoots.DataRoot;
            if (!string.IsNullOrWhiteSpace(dataBasePath))
            {
                string dataPath = Path.GetFullPath(Path.Combine(dataBasePath, path), fileRoots.WorkingDirectory);
                if (File.Exists(dataPath))
                    return dataPath;
            }

            string? serviceDirectory = fileRoots.ServiceDirectory;
            if (!string.IsNullOrWhiteSpace(serviceDirectory))
            {
                string servicePath = Path.GetFullPath(Path.Combine(serviceDirectory, path), fileRoots.WorkingDirectory);
                if (File.Exists(servicePath))
                    return servicePath;
            }

            return string.IsNullOrWhiteSpace(dataBasePath)
                ? Path.GetFullPath(path, fileRoots.WorkingDirectory)
                : Path.GetFullPath(Path.Combine(dataBasePath, path), fileRoots.WorkingDirectory);
        }

        internal static string? ValidateCorrectionMagnitudeFile(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return ValidateCorrectionMagnitudeFile(stream);
        }

        private static string? ValidateCorrectionMagnitudeFile(Stream stream)
        {
            const long headerSize = sizeof(ulong) + sizeof(float) + sizeof(int) + sizeof(ulong);
            if (stream.Length < headerSize)
                return $"新幅值标定文件格式错误：文件长度只有 {stream.Length} 字节。";

            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            ulong declaredLength = reader.ReadUInt64();
            float exposureTime = reader.ReadSingle();
            _ = reader.ReadInt32();
            ulong pointCount = reader.ReadUInt64();

            if (declaredLength != (ulong)stream.Length)
                return $"新幅值标定文件格式错误：文件头长度 {declaredLength} 与实际长度 {stream.Length} 不一致。";
            if (!float.IsFinite(exposureTime) || exposureTime <= 0)
                return $"新幅值标定文件格式错误：积分时间 {exposureTime} 无效。";
            if (pointCount != CorrectionSpectrumPointCount)
                return $"新幅值标定文件格式错误：运行时幅值标定要求 {CorrectionSpectrumPointCount} 点，实际为 {pointCount} 点。";

            ulong expectedLength = checked((ulong)headerSize + pointCount * 2UL * sizeof(double));
            if (expectedLength != (ulong)stream.Length)
                return $"新幅值标定文件格式错误：{pointCount} 点应为 {expectedLength} 字节，实际为 {stream.Length} 字节。";

            for (ulong index = 0; index < pointCount; index++)
            {
                double wavelength = reader.ReadDouble();
                double expectedWavelength = CorrectionSpectrumStart + CorrectionSpectrumInterval * index;
                if (!double.IsFinite(wavelength) || Math.Abs(wavelength - expectedWavelength) > CorrectionWavelengthTolerance)
                {
                    return $"新幅值标定文件格式错误：第 {index + 1} 个波长应为 {expectedWavelength:F1} nm，实际为 {wavelength:G17} nm。";
                }
            }

            for (ulong index = 0; index < pointCount; index++)
            {
                double coefficient = reader.ReadDouble();
                if (!double.IsFinite(coefficient) || coefficient < 0)
                    return $"新幅值标定文件格式错误：第 {index + 1} 个幅值系数无效。";
            }

            return null;
        }

        private static (double Start, double End, double Interval, int PointCount) ResolveCorrectionWavelengthMetadata(
            SpectumResultEntity entity,
            int availablePointCount) => ResolveCorrectionWavelengthMetadata(
                entity.fSpect1,
                entity.fSpect2,
                entity.fInterval,
                availablePointCount);

        internal static (double Start, double End, double Interval, int PointCount) ResolveCorrectionWavelengthMetadata(
            double? startValue,
            double? endValue,
            double? intervalValue,
            int availablePointCount)
        {
            double start = startValue ?? double.NaN;
            double end = endValue ?? double.NaN;
            double interval = intervalValue ?? double.NaN;
            if (!double.IsFinite(start) || !double.IsFinite(end) || !double.IsFinite(interval))
            {
                throw new InvalidOperationException(
                    $"数据库光谱波长元数据无效（Start={startValue}, End={endValue}, Interval={intervalValue}），拒绝猜测默认波长轴。");
            }

            if (Math.Abs(start - CorrectionSpectrumStart) > CorrectionWavelengthTolerance ||
                Math.Abs(end - CorrectionSpectrumEnd) > CorrectionWavelengthTolerance ||
                Math.Abs(interval - CorrectionSpectrumInterval) > CorrectionWavelengthTolerance)
            {
                throw new InvalidOperationException(
                    $"幅值校正仅接受 {CorrectionSpectrumStart:F0}–{CorrectionSpectrumEnd:F0} nm、{CorrectionSpectrumInterval:F1} nm 间隔的服务结果；实际为 Start={start:G17}, End={end:G17}, Interval={interval:G17}。");
            }

            if (availablePointCount < CorrectionSpectrumPointCount)
                throw new InvalidOperationException($"相对光谱数据点数不足：需要 {CorrectionSpectrumPointCount} 点，实际 {availablePointCount} 点。");

            return (CorrectionSpectrumStart, CorrectionSpectrumEnd, CorrectionSpectrumInterval, CorrectionSpectrumPointCount);
        }

        internal bool TryValidateMeasurementCalibrationFiles(out string rejectionReason)
        {
            Config.EnsureCalibrationGroups();
            SpectrumCalibrationGroup activeGroup = Config.ActiveCalibrationGroup;
            string wavelengthFile = string.IsNullOrWhiteSpace(Config.WavelengthFile)
                ? activeGroup.WavelengthFile
                : Config.WavelengthFile;
            string magnitudeFile = string.IsNullOrWhiteSpace(Config.MaguideFile)
                ? activeGroup.MaguideFile
                : Config.MaguideFile;

            try
            {
                string wavelengthPath = ResolveCalibrationFilePath(wavelengthFile, ServiceConfig.Instance.CVMainService_x64);
                string magnitudePath = ResolveCalibrationFilePath(magnitudeFile, ServiceConfig.Instance.CVMainService_x64);
                string? validationError = ValidateMeasurementCalibrationFiles(wavelengthPath, magnitudePath);
                if (validationError == null)
                {
                    rejectionReason = string.Empty;
                    return true;
                }

                rejectionReason = $"光谱校正文件检查未通过，已取消取图：\n\n{validationError}";
                log.Warn(rejectionReason);
                return false;
            }
            catch (Exception ex)
            {
                rejectionReason = $"光谱校正文件路径无效，已取消取图：{ex.Message}";
                log.Warn(rejectionReason, ex);
                return false;
            }
        }

        internal static string? ValidateMeasurementCalibrationFiles(string wavelengthPath, string magnitudePath)
        {
            var errors = new List<string>();
            SpectrumCalibrationFileValidationResult wavelengthResult = SpectrumCalibrationFileValidator.ValidateWavelengthFile(wavelengthPath);
            if (!wavelengthResult.IsValid)
                errors.Add(FormatCalibrationFileError(wavelengthResult, wavelengthPath));

            SpectrumCalibrationFileValidationResult magnitudeResult = SpectrumCalibrationFileValidator.ValidateMaguideFile(magnitudePath);
            if (!magnitudeResult.IsValid)
                errors.Add(FormatCalibrationFileError(magnitudeResult, magnitudePath));

            return errors.Count == 0 ? null : string.Join("\n\n", errors);
        }

        private static string FormatCalibrationFileError(SpectrumCalibrationFileValidationResult result, string path)
        {
            string displayPath = string.IsNullOrWhiteSpace(path) ? "（未配置）" : path;
            return $"{result.FileType}文件：{result.Message}\n路径：{displayPath}";
        }

        internal static string ResolveCalibrationFilePath(string path, string? mainServicePath)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            string? serviceDirectory = Path.GetDirectoryName(mainServicePath);
            if (string.IsNullOrWhiteSpace(serviceDirectory))
                return Path.GetFullPath(path);

            string pluginPath = Path.GetFullPath(Path.Combine(serviceDirectory, "plugin", "Spectrum", path));
            if (File.Exists(pluginPath))
                return pluginPath;

            string servicePath = Path.GetFullPath(Path.Combine(serviceDirectory, path));
            return File.Exists(servicePath) ? servicePath : pluginPath;
        }

        private string ResolveActiveMagnitudeFilePath()
        {
            Config.EnsureCalibrationGroups();
            string path = Config.MaguideFile;
            if (string.IsNullOrWhiteSpace(path))
                path = Config.ActiveCalibrationGroup.MaguideFile;
            if (SpectrumBackend.OpensLocally) return Local.LocalSpectrumSession.ResolvePath(path);
            return ResolveCalibrationFilePath(path, ServiceConfig.Instance.CVMainService_x64);
        }

        private static string ComputeFileSha256(string path)
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        private static void ShowCorrectionMessage(string message, MessageBoxImage image)
        {
            MessageBox.Show(
                Application.Current.GetActiveWindow(),
                message,
                "ColorVision",
                MessageBoxButton.OK,
                image);
        }

        [CommandDisplay("SpectrumLog", CategoryOrder = 3)]
        [Category("DataLogs")]
        [Description("SpectrumLogHint")]
        public RelayCommand OpenSpectrumLogCommand { get; set; }
        public static void OpenSpectrumLog()
        {
            string? mainServicePath = ServiceConfig.Instance.CVMainService_x64;
            string? baseDir = string.IsNullOrWhiteSpace(mainServicePath) ? null : Directory.GetParent(mainServicePath)?.FullName;
            if (string.IsNullOrWhiteSpace(baseDir))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), LocalizedText.Get("未配置光谱服务路径，无法定位光谱日志。"), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string? latestLogPath = ServiceLogFileLocator.GetMostRecentLogFile(Path.Combine(baseDir, "log"), "CVMainWindowsService_x64_Spectrum");
            if (!string.IsNullOrEmpty(latestLogPath))
            {
                WindowLogLocal windowLogLocal = new WindowLogLocal(latestLogPath, Encoding.GetEncoding("GB2312"));
                windowLogLocal.Show();
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), LocalizedText.Get("未找到光谱日志文件。"), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        [CommandDisplay("EditDisplayConfig", Order = -1, CategoryOrder = 0)]
        [Category("DeviceConnection")]
        [Description("SpectrumDisplayHint")]
        public RelayCommand EditDisplayConfigCommand { get; set; }
        public void EditDisplayConfig()
        {
            new PropertyEditorWindow(DisplayConfig) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public IReadOnlyList<HoleMap> GetNDHoleMappings()
        {
            var cfwPort = GetBoundCfwPort();
            var holeMapping = cfwPort?.FilterWheelConfig.HoleMapping;
            if (holeMapping != null && holeMapping.Count > 0)
                return holeMapping.ToList();

            return new FilterWheelConfig().HoleMapping.ToList();
        }

        public string? GetNDHoleName(int holeIndex)
        {
            return GetNDHoleMappings().FirstOrDefault(a => a.HoleIndex == holeIndex)?.HoleName;
        }

        public DeviceCfwPort? GetBoundCfwPort()
        {
            string deviceCode = Config.NDConfig.NDBindDeviceCode;
            if (string.IsNullOrWhiteSpace(deviceCode))
                return null;

            return ServiceManager.GetInstance().DeviceServices.OfType<DeviceCfwPort>().FirstOrDefault(a => a.Code == deviceCode);
        }

        internal bool TrySetNDPortForCalibrationGroup(int holeIndex, Local.LocalSpectrumOpticalScope optics, out MsgRecord? msgRecord, out string? error)
        {
            msgRecord = null;
            error = null;
            if (!optics.IsNdCurrent)
            {
                error = NdConfigurationChangedMessage;
                return false;
            }
            var nd = optics.Configuration.NDConfig;
            var cfwPort = optics.NdDevice;
            if (nd.IsNDPort && (SpectrumBackend.OpensLocally || cfwPort?.CfwBackend.OpensLocally == true))
            {
                msgRecord = RunLocalNdCommand("SetPort", holeIndex, optics);
                return true;
            }
            if (nd.IsBingNDDevice)
            {
                if (cfwPort == null)
                {
                    string deviceCode = nd.NDBindDeviceCode;
                    error = string.IsNullOrWhiteSpace(deviceCode)
                        ? "当前光谱仪未绑定 ND 滤光轮服务。"
                        : $"绑定的 ND 滤光轮服务 {deviceCode} 未加载。";
                    return false;
                }

                var service = cfwPort.DService;
                if (!optics.IsNdCurrent)
                {
                    error = NdConfigurationChangedMessage;
                    return false;
                }
                msgRecord = service.SetPort(holeIndex);
                return true;
            }

            msgRecord = DService.SendNdCommand(holeIndex, optics);
            return true;
        }

        public void OpenCalibrationGroupWindow()
        {
            Config.EnsureCalibrationGroups();
            new SpectrumCalibrationGroupWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            if (DisplayLazy.IsValueCreated)
                DisplayLazy.Value.RefreshNDHoleMappings();
        }

        public bool ApplyActiveCalibrationGroup(bool restartService)
        {
            return ApplyCalibrationGroup(Config.ActiveCalibrationGroup, restartService);
        }

        public bool ApplyCalibrationGroupForND(int holeIndex)
        {
            return ApplyCalibrationGroupForND(holeIndex, false);
        }

        public bool ApplyCalibrationGroupForND(int holeIndex, bool forceRestart)
        {
            var group = Config.FindCalibrationGroupForND(holeIndex, GetNDHoleName(holeIndex));
            return ApplyCalibrationGroup(group, true, forceRestart);
        }

        public bool ApplyCalibrationGroup(SpectrumCalibrationGroup? group, bool restartService, bool forceRestart = false)
        {
            return group != null && TryApplyCalibrationSelection(CaptureConfigurationTarget(), group.GroupName,
                group.WavelengthFile, group.MaguideFile, restartService, forceRestart, true, out bool changed) && changed;
        }

        internal bool TrySaveCalibrationGroupConfiguration(SpectrumCalibrationGroup? group, bool ndBindingChanged,
            DeviceConfigurationTarget target)
        {
            if (!TryApplyCalibrationSelection(target, group?.GroupName, group?.WavelengthFile ?? Config.WavelengthFile,
                group?.MaguideFile ?? Config.MaguideFile, true, false, true, out bool changed, persistUnchanged: true)) return false;
            if (!changed && ndBindingChanged)
            {
                if (!SpectrumBackend.OpensLocally) QueueCalibrationRestart(target);
                if (IsConfigurationTargetCurrent(target)) NotifyConfigChanged();
            }
            return true;
        }

        internal bool IsConfigurationTargetCurrent(DeviceConfigurationTarget target) => !localDisposed && target.IsCurrent(this);

        internal bool ApplyCalibrationGroupForND(int holeIndex, bool forceRestart, DeviceConfigurationTarget target,
            Local.LocalSpectrumOpticalScope? optics = null)
        {
            if (optics?.IsNdCurrent == false || !IsConfigurationTargetCurrent(target)) return false;
            var group = Config.FindCalibrationGroupForND(holeIndex, optics == null ? GetNDHoleName(holeIndex) : optics.GetNdHoleName(holeIndex));
            return ApplyCalibrationGroup(group, true, forceRestart, target);
        }

        internal bool ApplyCalibrationGroup(SpectrumCalibrationGroup? group, bool restartService, bool forceRestart, DeviceConfigurationTarget target)
        {
            return group != null && TryApplyCalibrationSelection(target, group.GroupName, group.WavelengthFile,
                group.MaguideFile, restartService, forceRestart, false, out _);
        }

        private bool TryApplyCalibrationSelection(DeviceConfigurationTarget target, string? groupName, string wavelengthFile,
            string magnitudeFile, bool restartService, bool forceRestart, bool saveFullConfiguration, out bool changed,
            bool persistUnchanged = false)
        {
            changed = false;
            if (!IsConfigurationTargetCurrent(target)) return false;

            using IDisposable applyScope = CalibrationGroupChangeGuard.EnterApply();
            changed = (groupName != null && !string.Equals(Config.ActiveCalibrationGroupName, groupName, StringComparison.Ordinal))
                || !string.Equals(Config.WavelengthFile, wavelengthFile, StringComparison.Ordinal)
                || !string.Equals(Config.MaguideFile, magnitudeFile, StringComparison.Ordinal) || forceRestart;
            if (!changed && !persistUnchanged) return true;

            string UpdateCalibration(string? previous)
            {
                JObject json;
                if (saveFullConfiguration) json = JObject.FromObject(Config);
                else if (string.IsNullOrWhiteSpace(previous)) json = new JObject();
                else
                {
                    using var reader = new JsonTextReader(new StringReader(previous)) { DateParseHandling = DateParseHandling.None };
                    json = JObject.Load(reader);
                }
                if (groupName != null)
                    json[nameof(ConfigSpectrum.ActiveCalibrationGroupName)] = groupName;
                json[nameof(ConfigSpectrum.WavelengthFile)] = wavelengthFile;
                json[nameof(ConfigSpectrum.MaguideFile)] = magnitudeFile;
                string? active = (string?)json[nameof(ConfigSpectrum.ActiveCalibrationGroupName)];
                if (json[nameof(ConfigSpectrum.CalibrationGroups)] is JArray groups && active != null)
                {
                    var selected = groups.OfType<JObject>().FirstOrDefault(entry =>
                        string.Equals((string?)entry[nameof(SpectrumCalibrationGroup.GroupName)], active, StringComparison.OrdinalIgnoreCase));
                    if (selected != null)
                    {
                        selected[nameof(SpectrumCalibrationGroup.WavelengthFile)] = wavelengthFile;
                        selected[nameof(SpectrumCalibrationGroup.MaguideFile)] = magnitudeFile;
                    }
                }
                return json.ToString(Formatting.None);
            }

            if (!target.TrySave(this, UpdateCalibration) || !IsConfigurationTargetCurrent(target)) return false;
            if (!changed) return true;
            if (groupName != null) Config.ActiveCalibrationGroupName = groupName;
            Config.WavelengthFile = wavelengthFile;
            Config.MaguideFile = magnitudeFile;

            if (restartService && !SpectrumBackend.OpensLocally)
                QueueCalibrationRestart(target);

            return true;
        }

        private bool RestartCalibrationService(DeviceConfigurationTarget target, CancellationTokenSource? request)
        {
            if (!IsConfigurationTargetCurrent(target) || SpectrumBackend.OpensLocally
                || !target.TryResolveRestart(this, out var route)) return false;

            lock (calibrationRestartSync)
            {
                if ((request != null && !ReferenceEquals(request, calibrationRestartCts)) || !IsConfigurationTargetCurrent(target)
                    || SpectrumBackend.OpensLocally) return false;
                return MqttRCService.GetInstance().TryRestartServices(route.TypeCode, route.ParentCode, route.DeviceCode);
            }
        }

        private void QueueCalibrationRestart(DeviceConfigurationTarget target)
        {
            var restartCts = new CancellationTokenSource();
            CancellationToken token = restartCts.Token;
            CancellationTokenSource? previousCts;

            lock (calibrationRestartSync)
            {
                if (localDisposed)
                {
                    restartCts.Dispose();
                    return;
                }
                previousCts = calibrationRestartCts;
                calibrationRestartCts = restartCts;
            }

            previousCts?.Cancel();
            previousCts?.Dispose();

            _ = RestartCalibrationServiceAfterIdleAsync(restartCts, token, target);
        }

        private async Task RestartCalibrationServiceAfterIdleAsync(CancellationTokenSource restartCts, CancellationToken token,
            DeviceConfigurationTarget target)
        {
            try
            {
                await Task.Delay(CalibrationRestartDebounceMilliseconds, token);
                await calibrationRestartGate.WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                if (RestartCalibrationService(target, restartCts))
                    await Task.Delay(CalibrationRestartCooldownMilliseconds);
            }
            catch (Exception ex)
            {
                log.Warn("Unable to restart the spectrum service after applying calibration.", ex);
            }
            finally
            {
                calibrationRestartGate.Release();
            }
        }

        public static int MyCallback(IntPtr strText, int nLen)
        {
            _ = Marshal.PtrToStringAnsi(strText, nLen);
            return 0;
        }

        public bool IsDiscoveringSpectrometers { get => _isDiscoveringSpectrometers; private set { _isDiscoveringSpectrometers = value; OnPropertyChanged(); } }
        private bool _isDiscoveringSpectrometers;

        public async Task GetSpectrSerialNumberAsync()
        {
            if (IsDiscoveringSpectrometers)
                return;
            IsDiscoveringSpectrometers = true;
            GetSpectrSerialNumberCommand.RaiseCanExecuteChanged();
            int port = int.TryParse(Config.ComPort, out int configuredPort) ? configuredPort : 0;
            try
            {
                var results = await Task.Run(() => SpectrumDeviceDiscovery.Discover(port, Spectrometer.CM_Emission_GetAllSN));
                foreach (SpectrumDiscoveryResult result in results.Where(result => result.Error != null))
                    log.Warn($"光谱仪搜索失败: Type={result.Type}, Port={result.ComPort}, NativeResult={result.NativeResult}, Error={result.Error}");
                MessageBox1.Show(Application.Current.GetActiveWindow(), SpectrumDeviceDiscovery.FormatResults(results), Properties.Resources.SpectrumSearchDevices);
            }
            finally
            {
                IsDiscoveringSpectrometers = false;
                GetSpectrSerialNumberCommand.RaiseCanExecuteChanged();
            }
        }

        internal static string GetSpectrumDriverToolPath(string baseDirectory) =>
            Path.Combine(baseDirectory, "Tools", "Spectrum", "zadig-2.4.exe");

        internal static bool HasExpectedSpectrumDriverToolHash(string filePath)
        {
            using FileStream stream = File.OpenRead(filePath);
            return Convert.ToHexString(SHA256.HashData(stream)).Equals(SpectrumDriverToolSha256, StringComparison.OrdinalIgnoreCase);
        }

        internal static void OpenSpectrumDriverTool()
        {
            Window? owner = Application.Current.GetActiveWindow();
            string title = GetSpectrumDriverToolText("SpectrumDriverTool", "光谱仪驱动工具");
            string toolPath = GetSpectrumDriverToolPath(AppContext.BaseDirectory);
            if (!File.Exists(toolPath))
            {
                MessageBox.Show(owner, string.Format(GetSpectrumDriverToolText("SpectrumDriverToolMissing", "未找到光谱仪驱动工具：{0}"), toolPath), title,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (!HasExpectedSpectrumDriverToolHash(toolPath))
                {
                    MessageBox.Show(owner, GetSpectrumDriverToolText("SpectrumDriverToolInvalid", "光谱仪驱动工具校验失败，请重新安装或修复 ColorVision。"), title,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                log.Error("校验光谱仪驱动工具失败", ex);
                MessageBox.Show(owner, string.Format(GetSpectrumDriverToolText("SpectrumDriverToolOpenFailed", "无法打开光谱仪驱动工具：{0}"), ex.Message), title,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (MessageBox.Show(owner, GetSpectrumDriverToolText("SpectrumDriverToolWarning",
                    "请仅选择 GCS Spectrameter，并确认目标驱动为 libusb-win32。不要选择键盘、鼠标、接收器、摄像头或 USB Hub。是否继续？"),
                    title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
                return;

            try
            {
                Process.Start(new ProcessStartInfo(toolPath)
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(toolPath) ?? AppContext.BaseDirectory,
                });
            }
            catch (Exception ex)
            {
                log.Error("打开光谱仪驱动工具失败", ex);
                MessageBox.Show(owner, string.Format(GetSpectrumDriverToolText("SpectrumDriverToolOpenFailed", "无法打开光谱仪驱动工具：{0}"), ex.Message), title,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string GetSpectrumDriverToolText(string key, string fallback) =>
            Properties.Resources.ResourceManager.GetString(key) ?? fallback;

        public void SelfAdaptionInitDark()
        {
            MsgRecord msgRecord = DService.SelfAdaptionInitDark();
            SelfAdaptionInitDarkStarted?.Invoke();
            msgRecord.MsgRecordStateChanged +=(s,e) =>
            {
                SelfAdaptionInitDarkCompleted?.Invoke();
                if (msgRecord.MsgReturn != null)
                    MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ExcAdaptiveZeroCali + e.ToString(), "ColorVison");
            };
        }

        public void SelfAdaptionInitDarkSetting() => EditImmediateConfiguration(Config.SelfAdaptionInitDark);

        public void EmissionSP100Setting() => EditImmediateConfiguration(Config.SetEmissionSP100Config);

        private void EditImmediateConfiguration(object settings)
        {
            var target = CaptureConfigurationTarget();
            new PropertyEditorWindow(settings, PropertyEditorEditMode.Immediate)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
            TrySaveConfig(Application.Current.GetActiveWindow(), target);
        }

        public void UploadLincense()
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            openFileDialog.Multiselect = true; // 允许多选
            openFileDialog.Filter = "All files (*.*)|*.zip;*.lic"; // 可以设置特定的文件类型过滤器
            openFileDialog.Title = ColorVision.Engine.Properties.Resources.SelectLicenseFilePrompt  + SysResourceModel.Code;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string[] selectedFiles = openFileDialog.FileNames;

                foreach (string file in selectedFiles)
                {
                    SetLicense(file);
                }
            }
        }

        public async Task UploadLicenseNet(string sn)
        {
            bool useLocal = SysResourceDao.IsLocalId(SysResourceModel.Id) || (SysResourceModel.Id <= 0 && SysResourceDao.Instance.UseLocal);
            try
            {
                var license = await new SpectrumLicenseUpdateService().DownloadAsync(sn);
                await Task.Run(() => PhySpectrumStore.SaveLicense(license, useLocal));
                log.Info($"Spectrum license updated: {sn}");
            }
            catch (Exception ex)
            {
                log.Error($"Spectrum license update failed: {sn}", ex);
            }
        }
        public  LicenseModel CameraLicenseModel { get; set; }
        public void SetLicense(string filepath)
        {
            if (!File.Exists(filepath)) return;
            if (Path.GetExtension(filepath) == ".zip")
            {
                try
                {
                    using ZipArchive archive = ZipFile.OpenRead(filepath);
                    var licFiles = archive.Entries.Where(entry => Path.GetExtension(entry.FullName).Equals(".lic", StringComparison.OrdinalIgnoreCase)).ToList();
                    foreach (var item in licFiles)
                    {
                        string Code = Path.GetFileNameWithoutExtension(item.FullName);
                        CameraLicenseModel = PhyLicenseDao.Instance.GetByMAC(Code);
                        if (CameraLicenseModel == null)
                            CameraLicenseModel = new LicenseModel();
                        CameraLicenseModel.DevCameraId = SysResourceModel.Id;
                        CameraLicenseModel.LiceType = 1;
                        CameraLicenseModel.MacAddress = Path.GetFileNameWithoutExtension(item.FullName);
                        using var stream = item.Open();
                        using var reader = new StreamReader(stream, Encoding.UTF8); // 假设文件编码为UTF-8
                        CameraLicenseModel.LicenseValue = reader.ReadToEnd();
                        CameraLicenseModel.CusTomerName = CameraLicenseModel.ColorVisionLicense.Licensee;
                        CameraLicenseModel.Model = CameraLicenseModel.ColorVisionLicense.DeviceMode;
                        CameraLicenseModel.ExpiryDate = CameraLicenseModel.ColorVisionLicense.ExpiryDateTime;
                        int ret = PhyLicenseDao.Instance.Save(CameraLicenseModel);
                        MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{CameraLicenseModel.MacAddress} {(ret == -1 ? ColorVision.Engine.Properties.Resources.AddFailed : ColorVision.Engine.Properties.Resources.AddSuccess)}", "ColorVision");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ExtractionFailed+$" :{ex.Message}", "ColorVision");
                }
            }
            else if (Path.GetExtension(filepath) == ".lic")
            {
                string Code = Path.GetFileNameWithoutExtension(filepath);
                CameraLicenseModel = PhyLicenseDao.Instance.GetByMAC(Code);
                if (CameraLicenseModel == null)
                    CameraLicenseModel = new LicenseModel();
                CameraLicenseModel.MacAddress = Path.GetFileNameWithoutExtension(filepath);
                CameraLicenseModel.LiceType = 1;
                CameraLicenseModel.LicenseValue = File.ReadAllText(filepath);
                CameraLicenseModel.CusTomerName = CameraLicenseModel.ColorVisionLicense.Licensee;
                CameraLicenseModel.Model = CameraLicenseModel.ColorVisionLicense.DeviceMode;
                CameraLicenseModel.ExpiryDate = CameraLicenseModel.ColorVisionLicense.ExpiryDateTime;

                int ret = PhyLicenseDao.Instance.Save(CameraLicenseModel);
                MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{CameraLicenseModel.MacAddress} {(ret == -1 ? ColorVision.Engine.Properties.Resources.AddFailed : ColorVision.Engine.Properties.Resources.UpdataSucess)}", "ColorVision");
            }
            else
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), ColorVision.Engine.Properties.Resources.UnsupportedLicenseFileExtension, "ColorVision");
            }
        }

        public void RefreshDeviceId()
        {
            MsgRecord msgRecord = DService.GetAllSnID();
            msgRecord.MsgRecordStateChanged += (s,e) =>
            {
                if (msgRecord.MsgReturn != null)
                {
                    List<string> strings = new List<string>();
                    foreach (var item in SysResourceDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "type", 103 } }))
                    {
                        strings.Add(item.Code);
                    }
                    string result = string.Join(",", strings);
                    MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.AllSpectrumDeviceInfo + Environment.NewLine + result);
                }
            };

        }
        public override UserControl GetDeviceInfo() => new InfoSpectrum(this);

        public Lazy<DisplaySpectrum> DisplayLazy { get; set; }
        public override UserControl GetDisplayControl() => DisplayLazy.Value;
        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
