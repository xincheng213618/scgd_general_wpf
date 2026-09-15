using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Common.MVVM;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using cvColorVision;
using FlowEngineLib;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.ComponentModel;
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

    [STNode("Flow_CustomNodes", "相机取图")]
    [FlowNodeDocumentation(
        "Flow_LocalCamera_Summary",
        Usage = "Flow_LocalCamera_Usage",
        Processing = "Flow_LocalCamera_Processing",
        Notes = "Flow_LocalCamera_Notes")]
    public sealed class LocalCameraNode : LocalFlowNodeBase
    {
        private const int CameraMasterResultType = 100;
        private string _CalibTempName = string.Empty;
        private float _ExpTime = 100;
        private float _Gain;
        private int _AvgCount = 1;
        private bool _AutoConnect = true;
        private bool _IsAutoExp;
        private bool _SaveFiles;
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
        [STNodeProperty("保存文件", "按本地相机规则保存 CVRAW，并在有校正数据时保存 CVCIE", true)]
        public bool SaveFiles { get => _SaveFiles; set { _SaveFiles = value; OnPropertyChanged(); } }

        [Category("本地相机")]
        [STNodeProperty("图像翻转", "X=上下翻转，Y=左右镜像，XY=180°（不支持 90°/270°旋转）。空间/普通校正始终先执行；有色度校正时翻转最终 CIE，否则翻转校正后的 RAW。未选择校正模板时保留方向配置，等待下游本地校正后应用；POI 使用最终方向的坐标。", true)]
        public CVImageFlipMode FlipMode { get => _FlipMode; set { _FlipMode = value; OnPropertyChanged(); } }

        [JsonIgnore]
        [CommandDisplay("相机管理", Order = -100)]
        [Description("打开本地相机管理窗口，并与当前节点同步取图参数")]
        public RelayCommand OpenLocalCameraManagerCommand { get; }

        [JsonIgnore]
        [CommandDisplay("校正缓存", Order = -90)]
        [Description("查看已缓存的校正文件、内存占用，并可释放本机校正缓存")]
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

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            DeviceCamera device = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>()
                .FirstOrDefault(camera => string.Equals(camera.Code, DeviceCode, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"找不到本地相机设备：{DeviceCode}");
            CameraRunParam cameraParameters = BuildCameraParameters();
            CalibrationParam? calibration = ResolveCalibration(device);
            if (CalibrationGroupGainResolver.TryResolve(calibration, device.PhyCamera?.VisualChildren.OfType<GroupResource>() ?? Enumerable.Empty<GroupResource>(), out float calibrationGain, out _))
                cameraParameters.Gain = calibrationGain;
            FlowNodeTiming.Run("ConnectCamera", () => device.EnsureLocalMeasurementConnected(AutoConnect));
            LocalCameraCaptureResult capture = LocalCameraCaptureService.Capture(new LocalCameraCaptureRequest
            {
                Device = device,
                CameraParameters = cameraParameters,
                Calibration = calibration,
                FlipMode = FlipMode,
                IsAutoExposure = IsAutoExp,
                SaveFiles = SaveFiles
            });

            LocalFlowFrame frame = capture.Frame;
            try
            {
                MeasureResultImgModel persistedResult = FlowNodeTiming.Run("PersistResult", () => LocalCameraResultService.SaveFlowModel(action, ZIndex, frame, capture, cameraParameters, calibration, IsAutoExp));
                int masterId = persistedResult.Id;
                frame.MasterId = masterId;
                action.MasterValue(null, masterId, CameraMasterResultType);
                FlowNodeTiming.Run("PublishPreview", () => device.PublishLocalPreview(frame, persistedResult, forceDisplay: false));
                action.SetCurrentFrame(frame);
                LocalFlowFrame currentFrame = frame;
                frame = null!;
                FlowNodeTiming.Run("PublishResult", () => ResultMessageBus.Default.PublishPersisted(ResultRoutes.Camera, ResultKinds.Image, persistedResult.DeviceCode ?? string.Empty, OperatorCode, action.SerialNumber, NodeID, ZIndex, masterId, CameraMasterResultType));
                LocalCameraNodeResultData result = new()
                {
                    FrameId = currentFrame.FrameId.ToString("N"),
                    TotalTime = capture.TotalTimeMs,
                    CaptureTime = capture.CaptureTimeMs,
                    CalibrationTime = capture.CalibrationTimeMs,
                    FlipMode = currentFrame.Metadata.FlipMode.ToString(),
                    FlipApplied = currentFrame.IsFlipApplied,
                    FlipDeferred = currentFrame.Metadata.FlipMode != CVImageFlipMode.None && !currentFrame.Metadata.IsMirrorReady,
                    SaveTime = capture.SaveTimeMs,
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
            return JsonConvert.SerializeObject(new { ServiceName = NodeName, DeviceCode, EventName = OperatorCode, action.SerialNumber, ExpTime, Gain, AvgCount, CalibTempName, FlipMode, AutoConnect, IsAutoExp, SaveFiles });
        }

        internal CameraRunParam BuildCameraParameters()
        {
            if (!float.IsFinite(ExpTime) || ExpTime <= 0)
                throw new InvalidOperationException("曝光时间必须大于 0。");
            if (!float.IsFinite(Gain) || Gain < 0)
                throw new InvalidOperationException("增益不能小于 0。");
            if (AvgCount < 1)
                throw new InvalidOperationException("平均次数必须大于或等于 1。");

            var cameraParameters = new CameraRunParam
            {
                Gain = Gain,
                AvgCount = AvgCount
            };
            cameraParameters.SetAllExposure(ExpTime);
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
