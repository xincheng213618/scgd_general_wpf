using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.PropertyEditor;
using ColorVision.FileIO;
using System.Diagnostics;
using ColorVision.Common.MVVM;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.Results;
using ColorVision.Themes.Controls;
using FlowEngineLib;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using FlowEngineLib.Repeat;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing.Nodes
{
    internal sealed class LocalCameraNodeResultData
    {
        public string FrameId { get; init; } = string.Empty;
        public int TotalTime { get; init; }
        public int CaptureTime { get; init; }
        public int CalibrationTime { get; init; }
        public string FlipMode { get; init; } = "None";
        public bool FlipApplied { get; init; }
        public bool FlipDeferred { get; init; }
        public int SaveTime { get; init; }
        public string CalibrationBackend { get; init; } = "None";
        public int MasterId { get; init; }
        public int MasterResultType { get; init; } = 100;
        public string? MasterValue { get; init; }
        public bool HasRaw { get; init; }
        public bool HasCie { get; init; }
        public string? CvRawFilePath { get; init; }
        public string? CvCieFilePath { get; init; }
    }

    [STNode("Flow_LocalDevices", "相机取图", CategoryOrder = 9900, NodeOrder = 10)]
    [FlowNodeDocumentation(
        "Flow_LocalCamera_Summary",
        Usage = "Flow_LocalCamera_Usage",
        Processing = "Flow_LocalCamera_Processing",
        Notes = "Flow_LocalCamera_Notes")]
    public sealed class LocalCameraNode : LocalDeviceFlowNodeBase, IRepeatParameterNode
    {
        private sealed record CameraExecutionParameters(DeviceCamera Device, CameraRunParam CameraParameters, CalibrationParam? Calibration,
            CVImageFlipMode FlipMode, bool IsAutoExp, bool AutoConnect, bool SaveFiles, string CalibTempName);
        private const int CameraMasterResultType = 100;
        private string _CalibTempName = string.Empty;
        private float _ExpTime = 100;
        private float _Gain;
        private int _AvgCount = 1;
        private bool _AutoConnect = true;
        private bool _IsAutoExp;
        private bool _SaveFiles = true;
        private CVImageFlipMode _FlipMode = CVImageFlipMode.None;

        [Category("本地相机")]
        [STNodeProperty("曝光时间(ms)", "本次取图使用的曝光时间；三通道相机的 R/G/B 使用相同值", true)]
        public float ExpTime { get => _ExpTime; set { _ExpTime = value; OnPropertyChanged(); } }

        [Category("本地相机")]
        [STNodeProperty("增益", "本次取图使用的相机增益", true)]
        public float Gain { get => _Gain; set { _Gain = value; OnPropertyChanged(); } }

        [Category("本地相机")]
        [STNodeProperty("平均次数", "单次流程执行中用于平均的采集次数，最小为 1", true)]
        public int AvgCount { get => _AvgCount; set { _AvgCount = value; OnPropertyChanged(); } }

        [Category("本地相机")]
        [STNodeProperty("校正模板", "取图时使用的校正模板；为空时只输出 CVRAW", true)]
        [PropertyEditorType(typeof(CalibrationTemplatePropertiesEditor))]
        public string CalibTempName { get => _CalibTempName; set { _CalibTempName = value ?? string.Empty; OnPropertyChanged(); } }

        [Category("本地相机")]
        [STNodeProperty("自动连接", "执行取图前，相机未连接时按设备当前配置自动尝试连接", true)]
        public bool AutoConnect { get => _AutoConnect; set { _AutoConnect = value; OnPropertyChanged(); } }

        [Category("本地相机")]
        [STNodeProperty("自动曝光", "启用相机本地自动曝光", true)]
        public bool IsAutoExp { get => _IsAutoExp; set { _IsAutoExp = value; OnPropertyChanged(); } }

        [Category("本地相机")]
        [STNodeProperty("保存文件", "启用时保存 CVRAW，写入完成后继续；关闭时仅保留缓存。数据库记录始终先写入，后续色度参数沿用此设置。", true)]
        public bool SaveFiles { get => _SaveFiles; set { _SaveFiles = value; OnPropertyChanged(); } }

        [Category("本地相机")]
        [STNodeProperty("图像翻转", "X=上下翻转，Y=左右镜像，XY=180°（不支持 90°/270°旋转）。空间/普通校正始终先执行，再翻转校正后的 RAW；色度保留参数供下游按需计算。未选择校正模板时保留方向配置，等待下游本地校正后应用；POI 使用最终方向的坐标。", true)]
        public CVImageFlipMode FlipMode { get => _FlipMode; set { _FlipMode = value; OnPropertyChanged(); } }

        [JsonIgnore]
        [CommandDisplay("相机管理", Order = -100)]
        [Description("打开本地相机管理窗口，并与当前节点同步取图参数")]
        public RelayCommand OpenLocalCameraManagerCommand { get; }

        [JsonIgnore]
        [CommandDisplay("缓存管理", Order = -90)]
        [Description("查看校正文件与图像文件缓存，并统一释放内存")]
        public RelayCommand OpenLocalCalibrationCacheManagerCommand { get; }

        public LocalCameraNode() : base("相机取图", "Camera", "GetData")
        {
            OpenLocalCameraManagerCommand = new RelayCommand(_ => OpenLocalCameraManager());
            OpenLocalCalibrationCacheManagerCommand = new RelayCommand(_ => LocalCalibrationCacheManagerWindow.OpenWindow());
            SelectFirstAvailableDevice<DeviceCamera>();
        }

        protected override string GetCompactSummaryValue() => $"{ExpTime:0.###} ms";

        private void OpenLocalCameraManager()
        {
            DeviceCamera? device = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>()
                .FirstOrDefault(camera => string.Equals(camera.Code, DeviceCode, StringComparison.Ordinal));
            if (device == null)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), $"找不到本地相机设备：{DeviceCode}", "ColorVision");
                return;
            }

            device.OpenLocalCameraWindow(this);
        }

        public IReadOnlyList<RepeatParameterDefinition> GetRepeatParameters() => new[]
        {
            new RepeatParameterDefinition(nameof(ExpTime), "曝光时间", "ms", ExpTime, minimum: float.Epsilon, maximum: float.MaxValue,
                unavailableReason: IsAutoExp ? "请先关闭相机节点的自动曝光，再设置逐轮曝光。" : null),
            new RepeatParameterDefinition(nameof(AvgCount), "平均次数", "次", AvgCount, isInteger: true, minimum: 1, maximum: int.MaxValue)
        };

        protected override object CaptureExecutionParameters(CVStartCFC action)
        {
            DeviceCamera device = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>()
                .FirstOrDefault(camera => string.Equals(camera.Code, DeviceCode, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"找不到本地相机设备：{DeviceCode}");
            CameraRunParam cameraParameters = BuildCameraParameters(action);
            CalibrationParam? calibration = ResolveCalibration(device);
            if (CalibrationGroupGainResolver.TryResolve(calibration, device.PhyCamera?.VisualChildren.OfType<GroupResource>() ?? Enumerable.Empty<GroupResource>(), out float calibrationGain, out _))
                cameraParameters.Gain = calibrationGain;
            return new CameraExecutionParameters(device, cameraParameters, calibration, FlipMode, IsAutoExp, AutoConnect, SaveFiles, CalibTempName);
        }

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            CameraExecutionParameters parameters = GetExecutionParameters<CameraExecutionParameters>(action);
            DeviceCamera device = parameters.Device;
            CameraRunParam cameraParameters = parameters.CameraParameters;
            CalibrationParam? calibration = parameters.Calibration;
            FlowNodeTiming.Run("ConnectCamera", () => device.EnsureLocalMeasurementConnected(parameters.AutoConnect));
            LocalCameraCaptureResult capture = LocalCameraCaptureService.Capture(new LocalCameraCaptureRequest
            {
                Device = device,
                CameraParameters = cameraParameters,
                Calibration = calibration,
                FlipMode = parameters.FlipMode,
                IsAutoExposure = parameters.IsAutoExp,
                SaveFiles = false,
                AllowAcceleration = true
            });

            LocalFlowFrame frame = capture.Frame;
            try
            {
                frame.CvRawFilePath = LocalFrameFileService.CreateCapturePath(device.Config.FileServerCfg.DataBasePath, device.Code, frame.Metadata);
                Stopwatch saveTimer = Stopwatch.StartNew();
                LocalFrameFileService.SaveCapture(frame, frame.CvRawFilePath, parameters.SaveFiles ? CVFileSaveMode.Synchronous : CVFileSaveMode.MemoryOnly);
                int saveTime = checked((int)Math.Min(saveTimer.ElapsedMilliseconds, int.MaxValue));
                MeasureResultImgModel persistedResult = FlowNodeTiming.Run("PersistResult", () => LocalCameraResultService.SaveFlowModel(action, ZIndex, frame, capture, cameraParameters, calibration, parameters.IsAutoExp));
                int masterId = persistedResult.Id;
                frame.MasterId = masterId;
                action.MasterValue(null, masterId, CameraMasterResultType);
                action.SetCurrentFrame(frame);
                LocalFlowFrame currentFrame = frame;
                frame = null!;
                FlowNodeTiming.Run("PublishResult", () => ResultMessageBus.Default.Publish(new ResultMessage(ResultRoutes.Camera, ResultKinds.Image, device.Code, OperatorCode, action.SerialNumber, NodeID, ZIndex, new ResultReference(masterId, CameraMasterResultType), persistedResult)));
                LocalCameraNodeResultData result = new()
                {
                    FrameId = currentFrame.FrameId.ToString("N"),
                    TotalTime = checked(capture.TotalTimeMs + saveTime),
                    CaptureTime = capture.CaptureTimeMs,
                    CalibrationTime = capture.CalibrationTimeMs,
                    FlipMode = currentFrame.Metadata.FlipMode.ToString(),
                    FlipApplied = currentFrame.IsFlipApplied,
                    FlipDeferred = currentFrame.Metadata.FlipMode != CVImageFlipMode.None && !currentFrame.Metadata.IsMirrorReady,
                    SaveTime = saveTime,
                    CalibrationBackend = capture.CalibrationBackend,
                    MasterId = masterId,
                    HasRaw = currentFrame.HasRaw,
                    HasCie = currentFrame.HasCie,
                    CvRawFilePath = NullIfEmpty(currentFrame.CvRawFilePath),
                    CvCieFilePath = NullIfEmpty(currentFrame.CvCieFilePath)
                };
                return new LocalNodeExecutionResult { Data = result };
            }
            finally
            {
                frame?.Dispose();
            }
        }

        protected override string BuildRunPayload(CVStartCFC action)
        {
            CameraExecutionParameters parameters = GetExecutionParameters<CameraExecutionParameters>(action);
            return JsonConvert.SerializeObject(new { ServiceName = NodeName, DeviceCode = parameters.Device.Code, EventName = OperatorCode,
                action.SerialNumber, parameters.CameraParameters.ExpTime, parameters.CameraParameters.Gain, parameters.CameraParameters.AvgCount,
                parameters.CalibTempName, parameters.FlipMode, parameters.AutoConnect, parameters.IsAutoExp, parameters.SaveFiles, AllowAcceleration = true });
        }

        internal CVFileSaveMode SaveMode => SaveFiles ? CVFileSaveMode.Synchronous : CVFileSaveMode.MemoryOnly;

        internal CameraRunParam BuildCameraParameters(CVStartCFC? action = null)
        {
            if (action != null)
                foreach (RepeatParameterDefinition definition in GetRepeatParameters())
                {
                    double value = ResolveRepeatParameter(action, definition.Key, double.NaN);
                    if (!double.IsNaN(value) && !definition.TryValidate(value, out string error)) throw new InvalidOperationException(error);
                }
            float exposure = action == null ? ExpTime : checked((float)ResolveRepeatParameter(action, nameof(ExpTime), ExpTime));
            int average = action == null ? AvgCount : checked((int)ResolveRepeatParameter(action, nameof(AvgCount), AvgCount));
            if (!float.IsFinite(exposure) || exposure <= 0)
                throw new InvalidOperationException("曝光时间必须大于 0。");
            if (!float.IsFinite(Gain) || Gain < 0)
                throw new InvalidOperationException("增益不能小于 0。");
            if (average < 1)
                throw new InvalidOperationException("平均次数必须大于或等于 1。");

            var cameraParameters = new CameraRunParam
            {
                Gain = Gain,
                AvgCount = average
            };
            cameraParameters.SetAllExposure(exposure);
            return cameraParameters;
        }

        private CalibrationParam? ResolveCalibration(DeviceCamera device)
        {
            if (string.IsNullOrWhiteSpace(CalibTempName)) return null;
            return device.PhyCamera?.CalibrationParams.FirstOrDefault(item => string.Equals(item.Key, CalibTempName, StringComparison.Ordinal))?.Value
                ?? throw new InvalidOperationException($"找不到校正模板：{CalibTempName}");
        }

        private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
