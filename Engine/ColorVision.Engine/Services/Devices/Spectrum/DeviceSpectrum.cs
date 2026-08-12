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
        public bool IsLuminousFluxMode { get => _IsLuminousFluxMode; set { if (_IsLuminousFluxMode == value) return; _IsLuminousFluxMode = value; OnPropertyChanged(); IsIsLuminousFluxModeChanged?.Invoke(this, value); } }
        private bool _IsLuminousFluxMode;

        public event EventHandler<bool> IsIsLuminousFluxModeChanged;

        [PropertyVisibility(nameof(IsLuminousFluxMode))]
        public double Divisor { get => _Divisor; set { _Divisor = value; OnPropertyChanged(); } }
        private double _Divisor = 1.0;


        [Display(Name = "Engine_PG_AutoIntegration", ResourceType = typeof(Properties.Resources))]
        public bool IsAutoIntTime { get => _IsAutoIntTime; set { _IsAutoIntTime = value; OnPropertyChanged(); } }
        private bool _IsAutoIntTime;

        [DisplayName("IsEnableNd")]
        public bool IsWithND { get => _IsWithND; set { _IsWithND = value; OnPropertyChanged(); } }
        private bool _IsWithND;

        public bool IsAutoDark { get => _IsAutoDark; set { if (value) IsShutter = false; _IsAutoDark = value; OnPropertyChanged(); } }
        private bool _IsAutoDark;
        public bool IsShutter { get => _IsShutter; set { if (value) IsAutoDark = false; _IsShutter = value; OnPropertyChanged(); } }
        private bool _IsShutter;


        public double IntTime { get => _IntTime; set { _IntTime = value; OnPropertyChanged(); } }
        private double _IntTime = 100;

        [Display(Name = "Engine_PG_MaxIntegrationTime", ResourceType = typeof(Properties.Resources))]
        public double MaxIntTime { get => _MaxIntTime; set { _MaxIntTime = value; OnPropertyChanged(); } }
        private double _MaxIntTime = 6000;

        public int AveNum { get => _AveNum; set { _AveNum = value; OnPropertyChanged(); } }
        private int _AveNum = 1;

        [Display(Name = "Engine_PG_MaxAveragingCount", ResourceType = typeof(Properties.Resources))]
        public int MaxAveNum { get => _MaxAveNum; set { _MaxAveNum = value; OnPropertyChanged(); } }
        private int _MaxAveNum = 10;

        public int PortNum { get => _PortNum; set { _PortNum = value; OnPropertyChanged(); } }
        private int _PortNum = 1;




        public double V { get => _V; set { _V = value; OnPropertyChanged(); } }
        private double _V = 5;
        public double I { get => _I; set { _I = value; OnPropertyChanged(); } }
        private double _I = 1;


    }

    public class DeviceSpectrum : DeviceService<ConfigSpectrum>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DeviceSpectrum));
        private static readonly TimeSpan CorrectionRestartVerificationTimeout = TimeSpan.FromSeconds(30);
        private const double CorrectionSpectrumStart = 380d;
        private const double CorrectionSpectrumEnd = 780d;
        private const double CorrectionSpectrumInterval = 0.1d;
        private const int CorrectionSpectrumPointCount = 4001;
        private const double CorrectionWavelengthTolerance = 1e-6;
        private const int CalibrationRestartDebounceMilliseconds = 1000;
        private const int CalibrationRestartCooldownMilliseconds = 4000;
        private readonly object calibrationRestartSync = new object();
        private readonly object correctionReloadSync = new object();
        private readonly SemaphoreSlim calibrationRestartGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim correctionExecutionGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim correctionMeasurementGate = new SemaphoreSlim(1, 1);
        private CancellationTokenSource? calibrationRestartCts;
        private TaskCompletionSource<bool>? correctionReloadCompletion;
        private bool correctionReloadPending;
        private bool correctionReloadDepartureObserved;
        private DateTime correctionRestartRequestedAtUtc;
        private int spectrumContinuousMeasurementLease;
        private int spectrumContinuousStatusObserved;
        private int spectrumContinuousStopAcknowledged;

        public MQTTSpectrum DService { get; set; }
        private readonly Lazy<ViewSpectrum> _view;
        public ViewSpectrum View => _view.Value;
        public DisplaySpectrumConfig DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<DisplaySpectrumConfig>(Config.Code);

        public ObservableCollection<TemplateModel<SpectrumResourceParam>> SpectrumResourceParams { get; set; } = new ObservableCollection<TemplateModel<SpectrumResourceParam>>();
        public RelayCommand RefreshDeviceIdCommand { get; set; }

        [CommandDisplay("UploadLic")]
        public RelayCommand UploadLincenseCommand { get; set; }

        [CommandDisplay("AdaptiveZeroCalibration")]

        public RelayCommand SelfAdaptionInitDarkCommand { get; set; }

        [CommandDisplay("ApaptivezeroCaliSet")]
        public RelayCommand SelfAdaptionInitDarkSettingCommand { get; set; }

        [CommandDisplay("EmissionSP100Set")]
        public RelayCommand EmissionSP100SettingCommand { get; set; }

        public event Action SelfAdaptionInitDarkStarted;
        public event Action SelfAdaptionInitDarkCompleted;

        [CommandDisplay("GetSpectrSerialNumber")]
        public RelayCommand GetSpectrSerialNumberCommand { get; set; }

        [CommandDisplay("CalibrationGroup", Order = -4)]
        public RelayCommand OpenCalibrationGroupWindowCommand { get; set; }

        [CommandDisplay("ApplyCalibrationGroup", Order = -5)]
        public RelayCommand ApplyCalibrationGroupCommand { get; set; }

        [CommandDisplay("光谱修正", Order = -3)]
        [Description("使用服务测量结果进行完整光谱或单独亮度修正")]
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
                window.Submited +=(s,e)=>
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

            GetSpectrSerialNumberCommand = new RelayCommand(a => GetSpectrSerialNumber());
            EditDisplayConfigCommand = new RelayCommand(a => EditDisplayConfig());
            OpenCalibrationGroupWindowCommand = new RelayCommand(a => OpenCalibrationGroupWindow());
            ApplyCalibrationGroupCommand = new RelayCommand(a => ApplyActiveCalibrationGroup(true));
            OpenSpectrumCorrectionCommand = new RelayCommand(async _ => await OpenSpectrumCorrectionAsync());

            OpenSpectrumLogCommand = new RelayCommand(a => OpenSpectrumLog());
            ContextMenu.Items.Add(new MenuItem() { Header = "SpectrumLog", Command = OpenSpectrumLogCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = "CalibrationGroup", Command = OpenCalibrationGroupWindowCommand });
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
                ShowCorrectionMessage("光谱修正窗口已经打开。", MessageBoxImage.Information);
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
                ShowCorrectionMessage($"打开光谱修正功能失败：{ex.Message}", MessageBoxImage.Error);
            }
            finally
            {
                correctionExecutionGate.Release();
            }
        }

        private async Task<SpectrumMeasurementSnapshot> CaptureCorrectionMeasurementAsync(CancellationToken cancellationToken)
        {
            if (IsCorrectionReloadPending())
            {
                throw new InvalidOperationException(
                    "新幅值标定配置仍在等待光谱服务完成重启周期；在重新采集验证前禁止再次发起校正采集。");
            }

            if (DisplayConfig.IsLuminousFluxMode)
            {
                throw new InvalidOperationException(
                    "当前为 EQE/光通量模式，不能用于幅值光谱修正。请切换到普通亮度/色度光谱模式后重试。");
            }

            Config.EnsureCalibrationGroups();
            if (DisplayConfig.IsWithND || Config.ActiveCalibrationGroup.NDHoleIndex >= 0)
            {
                throw new InvalidOperationException(
                    "当前启用了 ND 测量或选择了 ND 标定组；首版仅支持普通光谱幅值修正，请切回普通标定组后重试。");
            }

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
            if (entity.DataType)
                throw new InvalidOperationException("本次返回的是 EQE 结果，不能用于幅值光谱修正。请切换到普通光谱测量模式后重试。");

            double[] relativeSpectrum = LoadCorrectionRelativeSpectrum(entity);
            (double start, double end, double interval, int pointCount) = ResolveCorrectionWavelengthMetadata(entity, relativeSpectrum.Length);
            double[] croppedSpectrum = relativeSpectrum.Take(pointCount).ToArray();

            if (croppedSpectrum.Any(value => !double.IsFinite(value) || value < 0))
                throw new InvalidOperationException("服务返回的相对光谱包含无效或负数值，不能用于光谱修正。");

            double absoluteScale = entity.fPlambda ?? double.NaN;
            if (!double.IsFinite(absoluteScale) || absoluteScale <= 0)
                throw new InvalidOperationException("服务返回的绝对光谱系数无效，不能用于光谱修正。");

            double photometricValue = entity.fPh ?? double.NaN;
            if (!double.IsFinite(photometricValue))
                throw new InvalidOperationException("服务返回的光度值无效，不能用于光谱修正。");

            string magnitudeFilePath = ResolveActiveMagnitudeFilePath();
            if (!File.Exists(magnitudeFilePath))
                throw new InvalidOperationException($"当前幅值标定文件不存在：{magnitudeFilePath}");
            string? sourceValidationError = ValidateCorrectionMagnitudeFile(magnitudeFilePath);
            if (sourceValidationError != null)
                throw new InvalidOperationException($"当前幅值标定文件不能用于修正：{sourceValidationError}");
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
                "Luminance",
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
                if (IsCorrectionReloadPending())
                {
                    return SpectrumCorrectionApplyResult.Failure(
                        "前一次幅值标定应用仍在等待光谱服务完成离线/恢复周期，请先等待服务恢复。");
                }

                Config.EnsureCalibrationGroups();
                if (DisplayConfig.IsWithND || Config.ActiveCalibrationGroup.NDHoleIndex >= 0)
                {
                    return SpectrumCorrectionApplyResult.Failure(
                        "当前启用了 ND 测量或选择了 ND 标定组，不能应用普通光谱幅值文件；请切回普通标定组并重新采集。");
                }

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
                    return SpectrumCorrectionApplyResult.Failure("缺少原幅值标定文件校验值，请重新采集并生成修正文件。");

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
                            "原幅值标定文件在采集后发生了变化，请重新采集并生成修正文件。");
                    }
                }

                string previousGroupMagnitudeFile = activeGroup.MaguideFile;
                string previousConfigMagnitudeFile = Config.MaguideFile;
                activeGroup.MaguideFile = generatedPath;
                Config.MaguideFile = generatedPath;
                bool configSaved;
                try
                {
                    configSaved = TrySaveConfig();
                }
                catch
                {
                    RestoreCorrectionMagnitudeConfiguration(activeGroup, previousGroupMagnitudeFile, previousConfigMagnitudeFile);
                    throw;
                }

                if (!configSaved)
                {
                    RestoreCorrectionMagnitudeConfiguration(activeGroup, previousGroupMagnitudeFile, previousConfigMagnitudeFile);
                    return SpectrumCorrectionApplyResult.Failure("保存新幅值标定配置失败，未请求重启服务。");
                }

                Task restartVerified = BeginCorrectionReloadVerification();
                bool restartRequested;
                try
                {
                    restartRequested = TryRestartRCService();
                }
                catch (Exception ex)
                {
                    log.Error("Failed to request the Spectrum service restart.", ex);
                    restartRequested = false;
                }

                if (!restartRequested)
                {
                    RestoreCorrectionMagnitudeConfiguration(activeGroup, previousGroupMagnitudeFile, previousConfigMagnitudeFile);
                    bool rollbackSaved;
                    try
                    {
                        rollbackSaved = TrySaveConfig();
                    }
                    catch (Exception ex)
                    {
                        log.Error("Failed to persist rollback after the Spectrum restart request failed.", ex);
                        rollbackSaved = false;
                    }

                    if (rollbackSaved)
                    {
                        CancelCorrectionReloadVerification();
                        return SpectrumCorrectionApplyResult.Failure("RC 服务不可用或重启请求未发送，已回滚幅值标定配置。");
                    }

                    activeGroup.MaguideFile = generatedPath;
                    Config.MaguideFile = generatedPath;
                    MarkCorrectionRestartRequested();
                    return SpectrumCorrectionApplyResult.PendingRestart(
                        generatedPath,
                        "RC 服务不可用且幅值标定配置回滚失败；配置状态未确认，校正采集保持锁定，请恢复 RC 后手动重启服务。");
                }

                    MarkCorrectionRestartRequested();
                    try
                    {
                        await restartVerified.WaitAsync(CorrectionRestartVerificationTimeout, cancellationToken);
                        return SpectrumCorrectionApplyResult.Success(
                            generatedPath,
                            "已观察到光谱服务完成重启周期；服务当前不返回 DAT 加载回执，请重新采集标准灯数据验证新文件是否生效。");
                    }
                    catch (TimeoutException)
                    {
                        return SpectrumCorrectionApplyResult.PendingRestart(
                            generatedPath,
                            "重启请求已发送，但尚未观察到光谱服务完整离线/恢复周期；确认前校正采集保持锁定。");
                    }
                    catch (OperationCanceledException)
                    {
                        return SpectrumCorrectionApplyResult.PendingRestart(
                            generatedPath,
                            "重启请求已发送，等待重启确认期间操作被关闭；确认服务完整离线/恢复前校正采集保持锁定。");
                    }
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

        internal static bool IsCorrectionRestartDepartureStatus(DeviceStatusType status) =>
            status is DeviceStatusType.Closed
                or DeviceStatusType.Closing
                or DeviceStatusType.Opening
                or DeviceStatusType.OffLine
                or DeviceStatusType.UnInit;

        internal static (bool DepartureObserved, bool RestartVerified) AdvanceCorrectionRestartVerification(
            bool departureObserved,
            DeviceStatusType status)
        {
            bool nextDepartureObserved = departureObserved || IsCorrectionRestartDepartureStatus(status);
            return (nextDepartureObserved, nextDepartureObserved && IsCorrectionCaptureReadyStatus(status));
        }

        private bool IsCorrectionReloadPending()
        {
            lock (correctionReloadSync)
                return correctionReloadPending;
        }

        internal bool TryEnterSpectrumMeasurement(out string rejectionReason)
        {
            if (IsCorrectionReloadPending())
            {
                rejectionReason = "幅值标定配置正在等待服务完成重启周期，暂时禁止光谱测量。";
                return false;
            }

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

        private Task BeginCorrectionReloadVerification()
        {
            lock (correctionReloadSync)
            {
                if (correctionReloadPending)
                    throw new InvalidOperationException("已有幅值标定文件正在等待服务重启确认。");

                correctionReloadPending = true;
                correctionReloadDepartureObserved = false;
                correctionRestartRequestedAtUtc = DateTime.MaxValue;
                correctionReloadCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                DService.DeviceStatusChanged -= DService_CorrectionReloadStatusChanged;
                DService.DeviceStatusChanged += DService_CorrectionReloadStatusChanged;
                return correctionReloadCompletion.Task;
            }
        }

        private void DService_CorrectionReloadStatusChanged(object? sender, DeviceStatusType status)
        {
            TaskCompletionSource<bool>? completion = null;
            lock (correctionReloadSync)
            {
                if (!correctionReloadPending || correctionRestartRequestedAtUtc == DateTime.MaxValue)
                    return;

                // MQTTDeviceService queues status notifications on the Dispatcher. Ignore an
                // event whose payload no longer matches the current status: it predates this
                // restart verification window and must not prove the new restart cycle.
                if (status != DService.DeviceStatus)
                    return;

                (correctionReloadDepartureObserved, bool restartVerified) = AdvanceCorrectionRestartVerification(
                    correctionReloadDepartureObserved,
                    status);
                if (!restartVerified)
                    return;

                correctionReloadPending = false;
                correctionReloadDepartureObserved = false;
                correctionRestartRequestedAtUtc = default;
                completion = correctionReloadCompletion;
                correctionReloadCompletion = null;
            }

            DService.DeviceStatusChanged -= DService_CorrectionReloadStatusChanged;
            completion?.TrySetResult(true);
        }

        private void CancelCorrectionReloadVerification()
        {
            TaskCompletionSource<bool>? completion;
            lock (correctionReloadSync)
            {
                correctionReloadPending = false;
                correctionReloadDepartureObserved = false;
                correctionRestartRequestedAtUtc = default;
                completion = correctionReloadCompletion;
                correctionReloadCompletion = null;
            }

            DService.DeviceStatusChanged -= DService_CorrectionReloadStatusChanged;
            completion?.TrySetResult(false);
        }

        private void MarkCorrectionRestartRequested()
        {
            lock (correctionReloadSync)
            {
                if (correctionReloadPending)
                    correctionRestartRequestedAtUtc = DateTime.UtcNow;
            }
        }

        private void RestoreCorrectionMagnitudeConfiguration(
            SpectrumCalibrationGroup activeGroup,
            string previousGroupMagnitudeFile,
            string previousConfigMagnitudeFile)
        {
            activeGroup.MaguideFile = previousGroupMagnitudeFile;
            // The legacy top-level value can differ from the active group in old configurations.
            Config.MaguideFile = previousConfigMagnitudeFile;
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
                    $"幅值修正仅接受 {CorrectionSpectrumStart:F0}–{CorrectionSpectrumEnd:F0} nm、{CorrectionSpectrumInterval:F1} nm 间隔的服务结果；实际为 Start={start:G17}, End={end:G17}, Interval={interval:G17}。");
            }

            if (availablePointCount < CorrectionSpectrumPointCount)
                throw new InvalidOperationException($"相对光谱数据点数不足：需要 {CorrectionSpectrumPointCount} 点，实际 {availablePointCount} 点。");

            return (CorrectionSpectrumStart, CorrectionSpectrumEnd, CorrectionSpectrumInterval, CorrectionSpectrumPointCount);
        }

        private string ResolveActiveMagnitudeFilePath()
        {
            Config.EnsureCalibrationGroups();
            string path = Config.ActiveCalibrationGroup.MaguideFile;
            if (string.IsNullOrWhiteSpace(path))
                path = Config.MaguideFile;
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            string? serviceDirectory = Path.GetDirectoryName(ServiceConfig.Instance.CVMainService_x64);
            if (string.IsNullOrWhiteSpace(serviceDirectory))
                return Path.GetFullPath(path);

            string pluginPath = Path.GetFullPath(Path.Combine(serviceDirectory, "plugin", "Spectrum", path));
            if (File.Exists(pluginPath))
                return pluginPath;

            string servicePath = Path.GetFullPath(Path.Combine(serviceDirectory, path));
            return File.Exists(servicePath) ? servicePath : pluginPath;
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

        [CommandDisplay("SpectrumLog")]
        public RelayCommand OpenSpectrumLogCommand { get; set; }
        public static void OpenSpectrumLog()
        {
            string? mainServicePath = ServiceConfig.Instance.CVMainService_x64;
            string? baseDir = string.IsNullOrWhiteSpace(mainServicePath) ? null : Directory.GetParent(mainServicePath)?.FullName;
            if (string.IsNullOrWhiteSpace(baseDir))
                return;

            string? latestLogPath = ServiceLogFileLocator.GetMostRecentLogFile(Path.Combine(baseDir, "log"), "CVMainWindowsService_x64_Spectrum");
            if (!string.IsNullOrEmpty(latestLogPath))
            {
                WindowLogLocal windowLogLocal = new WindowLogLocal(latestLogPath, Encoding.GetEncoding("GB2312"));
                windowLogLocal.Show();
            }
        }

        [CommandDisplay("EditDisplayConfig", Order =-1)]
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

        public void GetSpectrSerialNumber()
        {
            int i = 0;
            if (int.TryParse(Config.ComPort, out int z))
            {
                i = z;
            }
            int bufferLength = 1024;
            StringBuilder stringBuilder = new StringBuilder(bufferLength);

            int ret = Spectrometer.CM_Emission_GetAllSN((int)Config.SpectrometerType,i, stringBuilder, bufferLength);

            string raw = stringBuilder.ToString();
            string display = FormatSerialNumberResult(raw);
            MessageBox1.Show(Application.Current.GetActiveWindow(), display, "Sprectrum");
        }

        /// <summary>
        /// 将CM_Emission_GetAllSN返回的JSON格式化为用户友好的显示文本
        /// </summary>
        internal static string FormatSerialNumberResult(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return Properties.Resources.NoDeviceDetected;

            try
            {
                var token = Newtonsoft.Json.Linq.JToken.Parse(raw);
                var snList = new List<string>();

                if (token is Newtonsoft.Json.Linq.JArray arr)
                {
                    foreach (var item in arr)
                        snList.Add(item.ToString());
                }
                else if (token is Newtonsoft.Json.Linq.JObject obj)
                {
                    foreach (var prop in obj.Properties())
                        snList.Add(prop.Value.ToString());
                }
                else
                {
                    snList.Add(token.ToString());
                }

                if (snList.Count == 0)
                    return Properties.Resources.NoDeviceDetected;

                if (snList.Count == 1)
                    return string.Format(Properties.Resources.DeviceSerialNumber, snList[0]);

                return string.Format(Properties.Resources.DevicesDetected, snList.Count) + "\n" + string.Join("\n", snList.Select((sn, idx) => $"  {idx + 1}. {sn}"));
            }
            catch
            {
                // JSON解析失败，直接显示原始内容
                return raw;
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
                RefreshEmptySpectrum();
            };

        }
        public void RefreshEmptySpectrum()
        {
             Count = SysResourceDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "type", 103 } }).Where(a => string.IsNullOrWhiteSpace(a.Value)).ToList().Count;
        }

        public int Count { get => _Count; set { _Count = value; OnPropertyChanged(); } }
        private int _Count;

        public override UserControl GetDeviceInfo() => new InfoSpectrum(this);

        public Lazy<DisplaySpectrum> DisplayLazy { get; set; }
        public override UserControl GetDisplayControl() => DisplayLazy.Value;
        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
