using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.PhyCameras.Group;
using log4net;
using MQTTMessageLib.Camera;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ColorVision.Engine.Services.Devices.Calibration
{
    /// <summary>
    /// Runs the optimized process-local calibration backend for the compact calibration control.
    /// The MQTT service remains available as a compatibility backend for unsupported source formats.
    /// </summary>
    internal static class LocalFileCalibrationService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LocalFileCalibrationService));

        public static MeasureResultImgModel Calibrate(
            DeviceCalibration device,
            CalibrationParam calibration,
            string filePath,
            string serialNumber,
            IReadOnlyList<float> exposure)
        {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentNullException.ThrowIfNull(calibration);
            ArgumentNullException.ThrowIfNull(exposure);

            string fullPath = Path.GetFullPath(filePath);
            float[] normalizedExposure = NormalizeExposure(exposure);
            DeviceCamera cameraDevice = device.PhyCamera?.DeviceCamera
                ?? throw new InvalidOperationException("本地校正需要关联相机设备；请先配置相机，或在显示配置中切换到 MQTT。");
            if (!cameraDevice.TryGetCalibrationTemplateFiles(
                calibration,
                out IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles,
                out string? errorMessage))
            {
                throw new InvalidOperationException(errorMessage ?? "校正模板无效。");
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            using LocalFlowFrame frame = LocalFrameFileService.Load(fullPath, normalizedExposure, 1);
            if (!frame.HasRaw)
            {
                throw new NotSupportedException("本地校正需要 RAW、TIFF 或常规位图源；CVCIE 请切换到 MQTT 服务处理。");
            }

            bool hasBasicCalibration = !LocalFrameCalibrationService.IsColorOnlyTemplate(calibrationFiles);
            LocalFrameCalibrationService.CalibrateInPlace(
                frame,
                cameraDevice.LocalCalibrationCacheManager,
                calibrationFiles,
                calibration.Name,
                LocalCalibrationRoi.Resolve(cameraDevice.PhyCamera?.Config?.CameraCfg, frame.Metadata.Width, frame.Metadata.Height));
            LocalFrameFileService.SaveCapture(
                frame,
                device.Config.FileServerCfg.DataBasePath,
                device.Code,
                includeRaw: hasBasicCalibration);
            stopwatch.Stop();

            string outputPath = !string.IsNullOrWhiteSpace(frame.CvCieFilePath)
                ? frame.CvCieFilePath
                : frame.CvRawFilePath;
            if (string.IsNullOrWhiteSpace(outputPath) || !File.Exists(outputPath))
            {
                throw new IOException("本地校正已完成，但没有生成可用的输出文件。");
            }

            using LocalFlowFrameLease output = frame.Acquire();
            MeasureResultImgModel model = BuildResultModel(
                device,
                calibration,
                serialNumber,
                fullPath,
                outputPath,
                normalizedExposure,
                output,
                cameraDevice.LocalCalibrationCacheManager.BackendName,
                checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue)));
            TryPersist(serialNumber, model);
            return model;
        }

        private static float[] NormalizeExposure(IReadOnlyList<float> exposure)
        {
            if (exposure.Count == 0)
            {
                throw new InvalidOperationException("曝光时间不能为空。");
            }

            float[] normalized = new float[3];
            for (int index = 0; index < normalized.Length; index++)
            {
                normalized[index] = exposure[Math.Min(index, exposure.Count - 1)];
                if (!float.IsFinite(normalized[index]) || normalized[index] <= 0)
                {
                    throw new InvalidOperationException($"曝光时间必须是大于 0 的有限数值，通道 {index + 1} 当前为 {normalized[index]}。");
                }
            }
            return normalized;
        }

        private static MeasureResultImgModel BuildResultModel(
            DeviceCalibration device,
            CalibrationParam calibration,
            string serialNumber,
            string sourcePath,
            string outputPath,
            float[] exposure,
            LocalFlowFrameLease output,
            string backendName,
            int totalTime)
        {
            bool hasCie = output.HasCie;
            return new MeasureResultImgModel
            {
                BatchId = -1,
                ZIndex = -1,
                NDPort = null,
                Params = JsonConvert.SerializeObject(new
                {
                    Backend = backendName,
                    SourceFile = sourcePath,
                    TemplateParam = new { calibration.Id, calibration.Name },
                    DeviceParam = new { exp = exposure, gain = 1 }
                }),
                RawFile = Path.GetFileName(outputPath),
                FileUrl = outputPath,
                FileType = (sbyte)(hasCie ? CameraFileType.CIEFile : CameraFileType.RawFile),
                ImgFrameInfo = JsonConvert.SerializeObject(new
                {
                    bpp = hasCie ? output.Metadata.CieBpp : output.Metadata.SourceBpp,
                    width = output.Metadata.Width,
                    height = output.Metadata.Height,
                    channels = output.Metadata.Channels,
                    exposure,
                    serialNumber
                }),
                ResultCode = 0,
                Result = "ok",
                TotalTime = totalTime,
                DeviceCode = device.Code,
                CreateDate = DateTime.Now
            };
        }

        private static void TryPersist(string serialNumber, MeasureResultImgModel model)
        {
            if (!MySqlControl.GetInstance().IsConnect)
            {
                return;
            }

            try
            {
                MeasureBatchModel? batch = BatchResultMasterDao.Instance.GetByCode(serialNumber);
                if (batch == null)
                {
                    batch = new MeasureBatchModel
                    {
                        Name = serialNumber,
                        Code = serialNumber,
                        ArchiveStatus = ArchiveStatus.NotArchived,
                        CreateDate = DateTime.Now
                    };
                    if (BatchResultMasterDao.Instance.Save(batch) <= 0)
                    {
                        log.Warn($"Local calibration batch persistence failed: {serialNumber}");
                        return;
                    }
                }

                model.BatchId = batch.Id;
                if (MeasureImgResultDao.Instance.Save(model) <= 0)
                {
                    log.Warn($"Local calibration result persistence failed: {model.FileUrl}");
                }
            }
            catch (Exception ex)
            {
                log.Warn("Local calibration persistence failed; the generated file remains available.", ex);
            }
        }

    }
}
