using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using FlowEngineLib.Base;
using FlowEngineLib.PropertyEditor;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.ComponentModel;
using System.Linq;

namespace ColorVision.Engine.Templates.Flow.Nodes
{
    internal sealed class LocalCameraNodeResultData
    {
        public string FrameId { get; init; } = string.Empty;
        public int TotalTime { get; init; }
        public int MasterId { get; init; }
        public int MasterResultType { get; init; } = 100;
        public string? MasterValue { get; init; }
        public bool HasRaw { get; init; }
        public bool HasCie { get; init; }
        public string? CvRawFilePath { get; init; }
        public string? CvCieFilePath { get; init; }
    }

    [STNode("Flow_CustomNodes", "本地相机取图")]
    [FlowNodePropertyEditorAttribute(nameof(CamTempName), typeof(FlowCameraRunTemplateEditor))]
    [FlowNodePropertyEditorAttribute(nameof(CalibTempName), typeof(FlowCalibrationTemplateEditor))]
    public sealed class LocalCameraNode : LocalFlowNodeBase
    {
        private const int CameraMasterResultType = 100;
        private string _CamTempName = string.Empty;
        private string _CalibTempName = string.Empty;
        private bool _IsAutoExp;
        private bool _SaveFiles;

        [Category("本地相机")]
        [STNodeProperty("相机模板", "相机参数模板；为空时使用设备当前参数", true)]
        public string CamTempName { get => _CamTempName; set { _CamTempName = value ?? string.Empty; OnPropertyChanged(); } }

        [Category("本地相机")]
        [STNodeProperty("校正模板", "取图时使用的校正模板；为空时只输出 CVRAW", true)]
        public string CalibTempName { get => _CalibTempName; set { _CalibTempName = value ?? string.Empty; OnPropertyChanged(); } }

        [Category("本地相机")]
        [STNodeProperty("自动曝光", "启用相机本地自动曝光", true)]
        public bool IsAutoExp { get => _IsAutoExp; set { _IsAutoExp = value; OnPropertyChanged(); } }

        [Category("本地相机")]
        [STNodeProperty("保存文件", "按本地相机规则保存 CVRAW，并在有校正数据时保存 CVCIE", true)]
        public bool SaveFiles { get => _SaveFiles; set { _SaveFiles = value; OnPropertyChanged(); } }

        public LocalCameraNode() : base("本地相机取图", "Camera", "GetData", 60000)
        {
        }

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            DeviceCamera device = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>()
                .FirstOrDefault(camera => string.Equals(camera.Code, DeviceCode, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"找不到本地相机设备：{DeviceCode}");
            CameraRunParam? cameraParameters = ResolveCameraParameters();
            CalibrationParam? calibration = ResolveCalibration(device);
            LocalCameraCaptureResult capture = LocalCameraCaptureService.Capture(new LocalCameraCaptureRequest
            {
                Device = device,
                CameraParameters = cameraParameters,
                Calibration = calibration,
                IsAutoExposure = IsAutoExp,
                SaveFiles = SaveFiles
            });

            LocalFlowFrame frame = capture.Frame;
            try
            {
                int masterId = SaveMasterResult(action, frame, capture.TotalTimeMs, cameraParameters, calibration);
                frame.MasterId = masterId;
                action.MasterValue(null, masterId, CameraMasterResultType);
                action.SetCurrentFrame(frame);
                LocalFlowFrame currentFrame = frame;
                frame = null!;
                LocalCameraNodeResultData result = new()
                {
                    FrameId = currentFrame.FrameId.ToString("N"),
                    TotalTime = capture.TotalTimeMs,
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
            return JsonConvert.SerializeObject(new { ServiceName = NodeName, DeviceCode, EventName = operatorCode, action.SerialNumber, CamTempName, CalibTempName, IsAutoExp, SaveFiles });
        }

        private CameraRunParam? ResolveCameraParameters()
        {
            if (string.IsNullOrWhiteSpace(CamTempName)) return null;
            return TemplateCameraRunParam.Params.FirstOrDefault(item => string.Equals(item.Key, CamTempName, StringComparison.Ordinal))?.Value
                ?? throw new InvalidOperationException($"找不到相机模板：{CamTempName}");
        }

        private CalibrationParam? ResolveCalibration(DeviceCamera device)
        {
            if (string.IsNullOrWhiteSpace(CalibTempName)) return null;
            return device.PhyCamera?.CalibrationParams.FirstOrDefault(item => string.Equals(item.Key, CalibTempName, StringComparison.Ordinal))?.Value
                ?? throw new InvalidOperationException($"找不到校正模板：{CalibTempName}");
        }

        private int SaveMasterResult(CVStartCFC action, LocalFlowFrame frame, int totalTime, CameraRunParam? cameraParameters, CalibrationParam? calibration)
        {
            MeasureBatchModel batch = BatchResultMasterDao.Instance.GetByNameOrCode(action.SerialNumber)
                ?? throw new InvalidOperationException($"找不到流程批次：{action.SerialNumber}");
            string fileUrl = !string.IsNullOrWhiteSpace(frame.CvCieFilePath) ? frame.CvCieFilePath : frame.CvRawFilePath;
            MeasureResultImgModel model = new()
            {
                BatchId = batch.Id,
                ZIndex = ZIndex,
                NDPort = -1,
                Params = JsonConvert.SerializeObject(new
                {
                    frame.Metadata.SourceBpp,
                    frame.Metadata.Gain,
                    ExpTime = frame.Metadata.Exposure,
                    IsAutoExpTime = IsAutoExp,
                    CamParamTemplate = new { ID = cameraParameters?.Id ?? -1, Name = cameraParameters?.Name ?? string.Empty },
                    Calibration = new { ID = calibration?.Id ?? -1, Name = calibration?.Name ?? string.Empty }
                }),
                RawFile = NullIfEmpty(System.IO.Path.GetFileName(frame.CvRawFilePath)),
                FileUrl = NullIfEmpty(fileUrl),
                FileType = string.IsNullOrWhiteSpace(fileUrl) ? null : (sbyte?)(fileUrl.EndsWith(".cvcie", StringComparison.OrdinalIgnoreCase) ? 1 : 2),
                ImgFrameInfo = JsonConvert.SerializeObject(new { bpp = frame.Metadata.SourceBpp, width = frame.Metadata.Width, height = frame.Metadata.Height, channels = frame.Metadata.Channels, hasCie = frame.HasCie }),
                ResultCode = 0,
                Result = "ok",
                TotalTime = totalTime,
                DeviceCode = DeviceCode,
                CreateDate = DateTime.Now
            };
            int masterId = MeasureImgResultDao.Instance.SaveAndReturnId(model);
            if (masterId <= 0) throw new InvalidOperationException("保存本地相机结果记录失败。");
            return masterId;
        }

        private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
