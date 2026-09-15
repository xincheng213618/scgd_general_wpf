using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.PhyCameras.Group;
using Newtonsoft.Json;
using FlowEngineLib.Algorithm;
using System;
using ColorVision.Database;
using FlowEngineLib.Base;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    internal static class LocalCameraResultService
    {
        internal static MeasureResultImgModel SaveFlowModel(CVStartCFC action, int zIndex, LocalFlowFrame frame, LocalCameraCaptureResult capture,
            CameraRunParam? cameraParameters, CalibrationParam? calibration, bool isAutoExposure)
        {
            MeasureBatchModel batch = BatchResultMasterDao.Instance.GetByNameOrCode(action.SerialNumber)
                ?? throw new InvalidOperationException($"找不到流程批次：{action.SerialNumber}");
            MeasureResultImgModel model = CreateModel(batch.Id, zIndex, frame, capture, cameraParameters, calibration, isAutoExposure);
            int masterId = MeasureImgResultDao.Instance.SaveAndReturnId(model);
            if (masterId <= 0) throw new InvalidOperationException("保存本地相机结果记录失败。");
            model.Id = masterId;
            return model;
        }

        internal static MeasureResultImgModel CreateModel(int batchId, int zIndex, LocalFlowFrame frame, LocalCameraCaptureResult capture,
            CameraRunParam? cameraParameters, CalibrationParam? calibration, bool isAutoExposure)
        {
            string fileUrl = !string.IsNullOrWhiteSpace(frame.CvCieFilePath) ? frame.CvCieFilePath : frame.CvRawFilePath;
            bool? savedRawFileFlipApplied = string.IsNullOrWhiteSpace(frame.CvRawFilePath) ? null : frame.IsRawFlipApplied;
            bool? savedCieFileFlipApplied = string.IsNullOrWhiteSpace(frame.CvCieFilePath) ? null : frame.IsCieFlipApplied;
            bool? savedFileFlipApplied = !string.IsNullOrWhiteSpace(frame.CvCieFilePath)
                ? savedCieFileFlipApplied
                : savedRawFileFlipApplied;
            MeasureResultImgModel model = new()
            {
                BatchId = batchId,
                ZIndex = zIndex,
                NDPort = -1,
                Params = JsonConvert.SerializeObject(new
                {
                    frame.Metadata.SourceBpp,
                    frame.Metadata.Gain,
                    ExpTime = frame.Metadata.Exposure,
                    IsAutoExpTime = isAutoExposure,
                    FlipMode = frame.Metadata.FlipMode,
                    MemoryFlipApplied = frame.IsFlipApplied,
                    MemoryFlipDeferred = frame.Metadata.FlipMode != CVImageFlipMode.None && !frame.Metadata.IsMirrorReady,
                    SavedRawFileFlipApplied = savedRawFileFlipApplied,
                    SavedCieFileFlipApplied = savedCieFileFlipApplied,
                    CamParamTemplate = new { ID = cameraParameters?.Id ?? -1, Name = cameraParameters?.Name ?? string.Empty },
                    Calibration = new { ID = calibration?.Id ?? -1, Name = calibration?.Name ?? string.Empty, Backend = capture.CalibrationBackend },
                    Timing = new { Capture = capture.CaptureTimeMs, Calibration = capture.CalibrationTimeMs, Save = capture.SaveTimeMs, Total = capture.TotalTimeMs }
                }),
                RawFile = NullIfEmpty(System.IO.Path.GetFileName(frame.CvRawFilePath)),
                FileUrl = NullIfEmpty(fileUrl),
                FileType = string.IsNullOrWhiteSpace(fileUrl) ? null : (sbyte?)(fileUrl.EndsWith(".cvcie", StringComparison.OrdinalIgnoreCase) ? 1 : 2),
                ImgFrameInfo = JsonConvert.SerializeObject(new { bpp = frame.Metadata.SourceBpp, width = frame.Metadata.Width, height = frame.Metadata.Height, channels = frame.Metadata.Channels, hasCie = frame.HasCie, flipMode = frame.Metadata.FlipMode, memoryFlipApplied = frame.IsFlipApplied, savedFileFlipApplied }),
                ResultCode = 0,
                Result = "ok",
                TotalTime = capture.TotalTimeMs,
                DeviceCode = frame.Metadata.DeviceCode,
                CreateDate = DateTime.Now
            };
            return model;
        }
        private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
