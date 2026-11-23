using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using log4net;
using MQTTMessageLib.Camera;
using Newtonsoft.Json;
using System;
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

        public static MeasureResultImgModel Calibrate(LocalFileCalibrationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            string fullPath = request.SourcePath;
            float[] normalizedExposure = LocalFrameCalibrationService.NormalizeExposure(request.Exposure);
            foreach (var calibrationFile in request.CalibrationFiles)
            {
                if (!File.Exists(calibrationFile.FullPath))
                    throw new FileNotFoundException(string.Format(Properties.Resources.TemplateFileNotExist,
                        request.TemplateName, calibrationFile.DisplayName), calibrationFile.FullPath);
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            var plan = request.Cache.Prepare(request.CalibrationFiles, request.TemplateName);
            using LocalFlowFrame frame = LocalFrameFileService.Load(fullPath, normalizedExposure, 1);
            if (!frame.HasRaw)
                throw new NotSupportedException("本地校正需要 RAW、TIFF 或常规位图源；CVCIE 请切换到 MQTT 服务处理。");
            plan.Apply(frame, plan.OutputFor(LocalColorCalibrationOutput.FullCie), request.FallbackRoi);
            LocalFrameFileService.SaveColorParametersToInput(frame);
            string capturePath = LocalFrameFileService.CreateCapturePath(request.OutputRoot ?? string.Empty, request.DeviceCode, frame.Metadata);
            LocalFrameFileService.SaveCapture(frame, capturePath);
            stopwatch.Stop();

            string outputPath = frame.CvRawFilePath;
            if (string.IsNullOrWhiteSpace(outputPath) || !File.Exists(outputPath))
            {
                throw new IOException("本地校正已完成，但没有生成可用的输出文件。");
            }

            using LocalFlowFrameLease output = frame.Acquire();
            MeasureResultImgModel model = BuildResultModel(
                request,
                fullPath,
                outputPath,
                normalizedExposure,
                output,
                checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue)));
            TryPersist(request.PersistenceEndpoint, request.SerialNumber, model);
            return model;
        }

        private static MeasureResultImgModel BuildResultModel(
            LocalFileCalibrationRequest request,
            string sourcePath,
            string outputPath,
            float[] exposure,
            LocalFlowFrameLease output,
            int totalTime)
        {
            return new MeasureResultImgModel
            {
                BatchId = -1,
                ZIndex = -1,
                NDPort = null,
                Params = JsonConvert.SerializeObject(new
                {
                    Backend = request.Cache.BackendName,
                    SourceFile = sourcePath,
                    TemplateParam = new { Id = request.TemplateId, Name = request.TemplateName },
                    DeviceParam = new { exp = exposure, gain = 1 }
                }),
                RawFile = Path.GetFileName(outputPath),
                FileUrl = outputPath,
                FileType = (sbyte)CameraFileType.RawFile,
                ImgFrameInfo = JsonConvert.SerializeObject(new
                {
                    bpp = output.Metadata.SourceBpp,
                    width = output.Metadata.Width,
                    height = output.Metadata.Height,
                    channels = output.Metadata.Channels,
                    exposure,
                    serialNumber = request.SerialNumber
                }),
                ResultCode = 0,
                Result = "ok",
                TotalTime = totalTime,
                DeviceCode = request.DeviceCode,
                CreateDate = DateTime.Now
            };
        }

        internal static void TryPersist(MySqlConnectedEndpoint? endpoint, string serialNumber, MeasureResultImgModel model)
        {
            if (endpoint == null) return;

            try
            {
                using var db = endpoint.CreateDbClient();
                db.Ado.BeginTran();

                int batchId;
                int resultId;
                try
                {
                    var batches = db.Queryable<MeasureBatchModel>().Where(batch => batch.Code == serialNumber)
                        .Select(batch => new { batch.Id, batch.Code }).Take(2).ToList();
                    if (batches.Count > 1 || (batches.Count == 1 && !string.Equals(batches[0].Code, serialNumber, StringComparison.Ordinal)))
                        throw new InvalidOperationException($"无法唯一匹配本地校正批次号：{serialNumber}。");

                    batchId = batches.Count == 1 ? batches[0].Id : db.Insertable(new MeasureBatchModel
                    {
                        Name = serialNumber,
                        Code = serialNumber,
                        ArchiveStatus = ArchiveStatus.NotArchived,
                        CreateDate = DateTime.Now,
                    }).ExecuteReturnIdentity();
                    if (batchId <= 0) throw new InvalidOperationException("本地校正保存未取得有效批次 ID。");

                    // Keep the generated file's result independent of the database until both writes commit.
                    resultId = db.Insertable(CreateImageRow(model, batchId)).ExecuteReturnIdentity();
                    if (resultId <= 0) throw new InvalidOperationException("本地校正保存未取得有效结果 ID。");
                    db.Ado.CommitTran();
                }
                catch (Exception error)
                {
                    try { db.Ado.RollbackTran(); }
                    catch (Exception rollbackError)
                    {
                        throw new AggregateException("本地校正结果保存失败，数据库事务回滚也失败。", error, rollbackError);
                    }
                    throw;
                }

                model.BatchId = batchId;
                model.Id = resultId;
            }
            catch (Exception ex)
            {
                log.Warn($"Local calibration persistence failed; the generated file remains available. Batch={serialNumber}, File={model.FileUrl}", ex);
            }
        }

        private static MeasureResultImgModel CreateImageRow(MeasureResultImgModel model, int batchId) => new()
        {
            BatchId = batchId,
            ZIndex = model.ZIndex,
            NDPort = model.NDPort,
            Params = model.Params,
            SmuDataId = model.SmuDataId,
            IResult = model.IResult,
            VResult = model.VResult,
            RawFile = model.RawFile,
            FileUrl = model.FileUrl,
            FileType = model.FileType,
            ImgFrameInfo = model.ImgFrameInfo,
            ResultCode = model.ResultCode,
            Result = model.Result,
            TotalTime = model.TotalTime,
            DeviceCode = model.DeviceCode,
            CreateDate = model.CreateDate,
        };
    }
}
