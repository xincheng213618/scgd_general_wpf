#pragma warning disable CA1863,CS8601,CS8604
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services.Logging;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.Spectrum.Calibration;
using ColorVision.Engine.Services.Devices.Spectrum.Correction;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Extension;
using ColorVision.UI.LogImp;
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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Services.Devices.Spectrum
{
    public class DisplaySpectrumConfig : IDisplayConfigBase
    {
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


        [Display(Name = "Engine_PG_AutoIntegration", ResourceType = typeof(Properties.Resources))]
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

        [Display(Name = "Engine_PG_MaxIntegrationTime", ResourceType = typeof(Properties.Resources))]
        public double MaxIntTime { get => _MaxIntTime; set { _MaxIntTime = value; OnPropertyChanged(); } }
        private double _MaxIntTime = 6000;

        [DisplayName("AverageTimes")]
        public int AveNum { get => _AveNum; set { _AveNum = value; OnPropertyChanged(); } }
        private int _AveNum = 1;

        [Display(Name = "Engine_PG_MaxAveragingCount", ResourceType = typeof(Properties.Resources))]
        public int MaxAveNum { get => _MaxAveNum; set { _MaxAveNum = value; OnPropertyChanged(); } }
        private int _MaxAveNum = 10;

        [DisplayName("SpectrumNdPosition")]
        public int PortNum { get => _PortNum; set { _PortNum = value; OnPropertyChanged(); } }
        private int _PortNum = 1;




        [DisplayName("SpectrumVoltage")]
        public double V { get => _V; set { _V = value; OnPropertyChanged(); } }
        private double _V = 5;
        [DisplayName("SpectrumCurrent")]
        public double I { get => _I; set { _I = value; OnPropertyChanged(); } }
        private double _I = 1;


    }

    public class DeviceSpectrum : DeviceService<ConfigSpectrum>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DeviceSpectrum));
        private const double CorrectionSpectrumStart = 380d;
        private const double CorrectionSpectrumEnd = 780d;
        private const double CorrectionSpectrumInterval = 0.1d;
        private const int CorrectionSpectrumPointCount = 4001;
        private const double CorrectionWavelengthTolerance = 1e-6;
        private const int CalibrationRestartDebounceMilliseconds = 1000;
        private const int CalibrationRestartCooldownMilliseconds = 4000;
        private readonly object calibrationRestartSync = new object();
        private readonly SemaphoreSlim calibrationRestartGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim correctionExecutionGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim correctionMeasurementGate = new SemaphoreSlim(1, 1);
        private CancellationTokenSource? calibrationRestartCts;
        private int spectrumContinuousMeasurementLease;
        private int spectrumContinuousStatusObserved;
        private int spectrumContinuousStopAcknowledged;

        public MQTTSpectrum DService { get; set; }
        private readonly Lazy<ViewSpectrum> _view;
        public ViewSpectrum View => _view.Value;
        public DisplaySpectrumConfig DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<DisplaySpectrumConfig>(Config.Code);

        public ObservableCollection<TemplateModel<SpectrumResourceParam>> SpectrumResourceParams { get; set; } = new ObservableCollection<TemplateModel<SpectrumResourceParam>>();

        [CommandDisplay("RefreshDeviceList", Order = 1, CategoryOrder = 0)]
        [Category("DeviceConnection")]
        [Description("SpectrumRefreshHint")]
        public RelayCommand RefreshDeviceIdCommand { get; set; }

        [CommandDisplay("UploadLic", Order = 2, CategoryOrder = 0)]
        [Category("DeviceConnection")]
        [Description("SpectrumLicenseHint")]
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

        [CommandDisplay("CalibrationGroup", Order = 0, CategoryOrder = 1)]
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
            DService = new MQTTSpectrum(this);
            _view = new Lazy<ViewSpectrum>(() => Application.Current.Dispatcher.CheckAccess()
                ? new ViewSpectrum(this)
                : Application.Current.Dispatcher.Invoke(() => new ViewSpectrum(this)));
            this.SetIconResource("DISpectrumIcon");

            Config.EnsureCalibrationGroups();

            SpectrumResourceParam.Load(SpectrumResourceParams, SysResourceModel.Id);

            EditCommand = new RelayCommand(a =>
            {
                PropertyEditorWindow window = new PropertyEditorWindow(Config);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.Submitted +=(s,e)=>
                {
                    //2026.01.21 增加逻辑，如果切换了ND模式，则清空对应的绑定信息
                    if (Config.NDConfig.IsBingNDDevice)
                    {
                        Config.NDConfig.SzComName = string.Empty;
                    }
                    else
                    {
                        Config.NDConfig.NDBindDeviceCode = string.Empty;
                    }

                    Save();
                };
                window.ShowDialog();

            }, a => AccessControl.Check(PermissionMode.Administrator));

            DisplayLazy = new Lazy<DisplaySpectrum>(() => new DisplaySpectrum(this));

            RefreshDeviceIdCommand = new RelayCommand(a => RefreshDeviceId());
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
                var host = new SpectrumCorrectionHost(CaptureCorrectionMeasurementAsync, ApplyCorrectionMagnitudeFileAsync);
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

        private async Task<SpectrumMeasurementSnapshot> CaptureCorrectionMeasurementAsync(CancellationToken cancellationToken)
        {
            Config.EnsureCalibrationGroups();
            DeviceStatusType deviceStatus = DService.DeviceStatus;
            if (!IsCorrectionCaptureReadyStatus(deviceStatus))
            {
                string reason = deviceStatus switch
                {
                    DeviceStatusType.Opening => "光谱仪服务正在连接，请连接完成后再采集。",
                    DeviceStatusType.Closing => "光谱仪服务正在关闭，请稍后重试。",
                    DeviceStatusType.Busy => "光谱仪正在执行其他测量，请完成后再采集。",
                    DeviceStatusType.SP_Continuous_Mode => "光谱仪正在连续测量，请先停止连续测量。",
                    DeviceStatusType.Closed or DeviceStatusType.UnInit => "光谱仪尚未连接，请先打开服务连接。",
                    DeviceStatusType.OffLine => "光谱仪服务离线，暂时不能采集。",
                    DeviceStatusType.Unauthorized => "光谱仪未授权，暂时不能采集。",
                    _ => $"光谱仪当前状态为 {deviceStatus}，暂时不能进行单次采集。",
                };
                throw new InvalidOperationException(reason);
            }

            TimeSpan captureTimeout = CalculateCorrectionCaptureTimeout(
                DisplayConfig.IntTime,
                Config.MaxIntegralTime,
                DisplayConfig.AveNum,
                DisplayConfig.IsAutoIntTime,
                DisplayConfig.IsAutoDark || DisplayConfig.IsShutter);
            MsgRecord msgRecord;
            MsgRecordState state;
            try
            {
                if (!IsCorrectionCaptureReadyStatus(DService.DeviceStatus))
                    throw new InvalidOperationException("光谱仪状态已发生变化，请等待当前操作完成后重试。");

                msgRecord = DService.GetData(captureTimeout.TotalMilliseconds);
                state = await ScheduledDeviceJobHelper.WaitForTerminalStateAsync(
                    msgRecord,
                    captureTimeout,
                    cancellationToken);
            }
            catch (TimeoutException)
            {
                throw new InvalidOperationException($"光谱测量超时（{captureTimeout.TotalSeconds:0} 秒）。");
            }

            if (state != MsgRecordState.Success)
            {
                string detail = msgRecord.MsgReturn?.Message;
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
                    ? $"光谱测量失败（{state}）。"
                    : $"光谱测量失败：{detail}");
            }

            int masterId = GetCorrectionMasterId(msgRecord.MsgReturn);
            using var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true,
            });

            SpectumResultEntity? entity = db.Queryable<SpectumResultEntity>().Where(item => item.Id == masterId).First();
            if (entity == null)
                throw new InvalidOperationException($"未找到光谱测量结果（MasterId={masterId}）。");
            if (!string.Equals(entity.DeviceCode, Config.Code, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"光谱结果设备不匹配：期望 {Config.Code}，实际 {entity.DeviceCode ?? "<空>"}。");
            }
            double[] relativeSpectrum = LoadCorrectionRelativeSpectrum(entity);
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

            string magnitudeFilePath = ResolveActiveMagnitudeFilePath();
            if (!File.Exists(magnitudeFilePath))
                throw new InvalidOperationException($"当前幅值标定文件不存在：{magnitudeFilePath}");
            string? sourceValidationError = ValidateCorrectionMagnitudeFile(magnitudeFilePath);
            if (sourceValidationError != null)
                throw new InvalidOperationException($"当前幅值标定文件不能用于校正：{sourceValidationError}");
            string magnitudeFileSha256 = ComputeFileSha256(magnitudeFilePath);

            DateTime measuredAt = entity.CreateDate == default ? DateTime.Now : entity.CreateDate;
            return new SpectrumMeasurementSnapshot(
                entity.Id,
                entity.DeviceCode ?? Config.Code ?? string.Empty,
                Config.SN ?? string.Empty,
                new DateTimeOffset(measuredAt),
                start,
                end,
                interval,
                croppedSpectrum,
                absoluteScale,
                photometricValue,
                entity.IntTime ?? DisplayConfig.IntTime,
                entity.iAveNum ?? DisplayConfig.AveNum,
                entity.DataType ? "LuminousFlux" : "Luminance",
                Config.ActiveCalibrationGroupName ?? string.Empty,
                magnitudeFilePath,
                magnitudeFileSha256);
        }

        private async Task<SpectrumCorrectionApplyResult> ApplyCorrectionMagnitudeFileAsync(
            SpectrumCorrectionApplyRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                Config.EnsureCalibrationGroups();
                if (!await correctionMeasurementGate.WaitAsync(0, cancellationToken))
                    return SpectrumCorrectionApplyResult.Failure("光谱仪正在执行其他测量或校正操作，不能应用幅值标定文件。");

                try
                {
                    DeviceStatusType deviceStatus = DService.DeviceStatus;
                    if (!IsCorrectionCaptureReadyStatus(deviceStatus))
                    {
                        string reason = deviceStatus switch
                        {
                            DeviceStatusType.Busy => "光谱仪正在测量，不能应用幅值标定文件。",
                            DeviceStatusType.SP_Continuous_Mode => "光谱仪正在连续测量，请先停止连续测量再应用幅值标定文件。",
                            _ => $"光谱仪当前状态为 {deviceStatus}，不能安全应用幅值标定文件。",
                        };
                        return SpectrumCorrectionApplyResult.Failure(reason);
                    }

                    SpectrumCalibrationGroup activeGroup = Config.ActiveCalibrationGroup;
                if (!string.IsNullOrWhiteSpace(request.CalibrationGroupName) &&
                    !string.Equals(request.CalibrationGroupName, activeGroup.GroupName, StringComparison.OrdinalIgnoreCase))
                {
                    return SpectrumCorrectionApplyResult.Failure(
                        $"当前标定组已从“{request.CalibrationGroupName}”切换为“{activeGroup.GroupName}”，请重新采集后再应用。");
                }

                if (string.IsNullOrWhiteSpace(request.MagnitudeFilePath))
                    return SpectrumCorrectionApplyResult.Failure("未提供新幅值标定文件。");
                if (string.IsNullOrWhiteSpace(request.ExpectedSourceMagnitudeSha256))
                    return SpectrumCorrectionApplyResult.Failure("缺少原幅值标定文件校验值，请重新采集并生成校正文件。");

                string generatedPath = Path.GetFullPath(request.MagnitudeFilePath);
                if (!File.Exists(generatedPath))
                    return SpectrumCorrectionApplyResult.Failure($"新幅值标定文件不存在：{generatedPath}");

                string? validationError = ValidateCorrectionMagnitudeFile(generatedPath);
                if (validationError != null)
                    return SpectrumCorrectionApplyResult.Failure(validationError);

                if (!string.IsNullOrWhiteSpace(request.ExpectedSourceMagnitudeSha256))
                {
                    string currentSourcePath = ResolveActiveMagnitudeFilePath();
                    if (!File.Exists(currentSourcePath))
                        return SpectrumCorrectionApplyResult.Failure("原幅值标定文件已不存在，请重新采集后再应用。");

                    string currentHash = ComputeFileSha256(currentSourcePath);
                    if (!string.Equals(currentHash, request.ExpectedSourceMagnitudeSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        return SpectrumCorrectionApplyResult.Failure(
                            "原幅值标定文件在采集后发生了变化，请重新采集并生成校正文件。");
                    }
                }

                activeGroup.MaguideFile = generatedPath;
                Config.MaguideFile = generatedPath;
                Save();
                return SpectrumCorrectionApplyResult.Success(
                    generatedPath,
                    "新 DAT 已应用，服务正在重启；恢复后请重新采集验证。");
                }
                finally
                {
                    correctionMeasurementGate.Release();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                log.Error("Failed to apply corrected Spectrum magnitude file.", ex);
                return SpectrumCorrectionApplyResult.Failure($"应用新幅值标定文件失败：{ex.Message}");
            }
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
            EventHandler<MsgRecordState>? stateChanged = null;
            stateChanged = (_, state) =>
            {
                if (state is not (MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout))
                    return;

                if (Interlocked.Exchange(ref released, 1) != 0)
                    return;

                msgRecord.MsgRecordStateChanged -= stateChanged;
                correctionMeasurementGate.Release();
            };

            msgRecord.MsgRecordStateChanged += stateChanged;
            stateChanged(msgRecord, msgRecord.MsgRecordState);
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

        private double[] LoadCorrectionRelativeSpectrum(SpectumResultEntity entity)
        {
            string? json = entity.fPL;
            if (!string.IsNullOrWhiteSpace(entity.fPL_file_name))
            {
                string spectrumPath = ResolveSpectrumResultFilePath(entity.fPL_file_name);
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

        private string ResolveSpectrumResultFilePath(string path)
        {
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            string dataBasePath = Config.FileServerCfg.DataBasePath;
            if (!string.IsNullOrWhiteSpace(dataBasePath))
            {
                string dataPath = Path.GetFullPath(Path.Combine(dataBasePath, path));
                if (File.Exists(dataPath))
                    return dataPath;
            }

            string? serviceDirectory = Path.GetDirectoryName(ServiceConfig.Instance.CVMainService_x64);
            if (!string.IsNullOrWhiteSpace(serviceDirectory))
            {
                string servicePath = Path.GetFullPath(Path.Combine(serviceDirectory, path));
                if (File.Exists(servicePath))
                    return servicePath;
            }

            return string.IsNullOrWhiteSpace(dataBasePath)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(dataBasePath, path));
        }

        internal static string? ValidateCorrectionMagnitudeFile(string path)
        {
            const long headerSize = sizeof(ulong) + sizeof(float) + sizeof(int) + sizeof(ulong);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
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
        [Category("MaintenanceDiagnostics")]
        [Description("SpectrumLogHint")]
        public RelayCommand OpenSpectrumLogCommand { get; set; }
        public static void OpenSpectrumLog()
        {
            string? mainServicePath = ServiceConfig.Instance.CVMainService_x64;
            string? baseDir = string.IsNullOrWhiteSpace(mainServicePath) ? null : Directory.GetParent(mainServicePath)?.FullName;
            if (string.IsNullOrWhiteSpace(baseDir))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "未配置光谱服务路径，无法定位光谱日志。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show(Application.Current.GetActiveWindow(), "未找到光谱日志文件。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        [CommandDisplay("EditDisplayConfig", Order =-1, CategoryOrder = 2)]
        [Category("AcquisitionDisplay")]
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
            if (group == null)
                return false;

            bool changed = !string.Equals(Config.ActiveCalibrationGroupName, group.GroupName, StringComparison.Ordinal)
                || !string.Equals(Config.WavelengthFile, group.WavelengthFile, StringComparison.Ordinal)
                || !string.Equals(Config.MaguideFile, group.MaguideFile, StringComparison.Ordinal);

            Config.ActiveCalibrationGroupName = group.GroupName;
            Config.WavelengthFile = group.WavelengthFile;
            Config.MaguideFile = group.MaguideFile;

            if (!changed && !forceRestart)
                return false;

            SaveConfig();

            if (restartService)
                QueueCalibrationRestart();

            return true;
        }

        private void QueueCalibrationRestart()
        {
            var restartCts = new CancellationTokenSource();
            CancellationTokenSource? previousCts;

            lock (calibrationRestartSync)
            {
                previousCts = calibrationRestartCts;
                calibrationRestartCts = restartCts;
            }

            previousCts?.Cancel();
            previousCts?.Dispose();

            _ = RestartCalibrationServiceAfterIdleAsync(restartCts);
        }

        private async Task RestartCalibrationServiceAfterIdleAsync(CancellationTokenSource restartCts)
        {
            try
            {
                await Task.Delay(CalibrationRestartDebounceMilliseconds, restartCts.Token);
                await calibrationRestartGate.WaitAsync(restartCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                lock (calibrationRestartSync)
                {
                    if (!ReferenceEquals(restartCts, calibrationRestartCts))
                        return;
                }

                RestartRCService();
                await Task.Delay(CalibrationRestartCooldownMilliseconds);
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
            int.TryParse(Config.ComPort, out int port);
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

        public void SelfAdaptionInitDarkSetting()
        {
            new PropertyEditorWindow(Config.SelfAdaptionInitDark) { Owner =Application.Current.GetActiveWindow() ,WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            SaveConfig();
        }
        public void EmissionSP100Setting()
        {
            new PropertyEditorWindow(Config.SetEmissionSP100Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            SaveConfig();
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
            // 设置请求的URL和数据
            string url = "https://color-vision.picp.net/license/api/v1/license/onlyDownloadLicense";
            var postData = new { macSn = sn };
            string DirLicense = $"{Environments.DirAppData}\\Licenses";
            if (!Directory.Exists(DirLicense))
                Directory.CreateDirectory(DirLicense);

            string fileName = $"{DirLicense}\\{sn}-license.zip";

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // 发送POST请求
                    HttpResponseMessage response = await client.PostAsJsonAsync(url, postData);
                    // 检查响应状态码
                    response.EnsureSuccessStatusCode();

                    // 确保返回的是一个文件而不是JSON
                    if (response.Content.Headers.ContentType?.MediaType == "application/json")
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                    }
                    // 获取文件名
                    fileName = "license.zip"; // 默认文件名
                    if (response.Content.Headers.ContentDisposition != null)
                    {
                        fileName = response.Content.Headers.ContentDisposition.FileName?.Trim('"');
                    }
                    fileName = $"{DirLicense}\\{fileName}";
                    using (FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                    SetLicense(fileName);
                }
                catch 
                {

                }
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
                        Task.Run(() => UploadLicenseNet(item.Code));
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
