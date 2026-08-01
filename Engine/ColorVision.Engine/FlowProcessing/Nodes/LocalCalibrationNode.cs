using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Database;
using FlowEngineLib.Base;
using FlowEngineLib.PropertyEditor;
using MQTTMessageLib.Camera;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Nodes
{
    internal class LocalCalibrationNodeResultData
    {
        public string FrameId { get; init; } = string.Empty;
        public int MasterId { get; init; }
        public int MasterResultType { get; init; } = (int)ViewResultAlgType.Calibration;
        public int TotalTime { get; init; }
        public bool Calibrated { get; init; }
        public bool HasCieFile { get; init; }
        public string? CvCieFilePath { get; init; }
    }

    internal sealed class LocalCalibrationExecution : IDisposable
    {
        private bool ownsFrame;

        public LocalCalibrationExecution(
            LocalFlowFrame frame,
            bool ownsFrame,
            CalibrationParam? calibration,
            string sourceFrameId,
            bool loadedFromFile,
            bool calibrated,
            int totalTime)
        {
            Frame = frame;
            this.ownsFrame = ownsFrame;
            Calibration = calibration;
            SourceFrameId = sourceFrameId;
            LoadedFromFile = loadedFromFile;
            Calibrated = calibrated;
            TotalTime = totalTime;
        }

        public LocalFlowFrame Frame { get; }
        public CalibrationParam? Calibration { get; }
        public string SourceFrameId { get; }
        public bool LoadedFromFile { get; }
        public bool Calibrated { get; }
        public int TotalTime { get; }

        public void TransferFrameTo(CVStartCFC action)
        {
            action.SetCurrentFrame(Frame);
            ownsFrame = false;
        }

        public void Dispose()
        {
            if (ownsFrame)
            {
                Frame.Dispose();
                ownsFrame = false;
            }
        }
    }

    public abstract class LocalCalibrationNodeBase : LocalFlowNodeBase
    {
        private string calibTempName = string.Empty;
        private bool saveFiles;

        [Category("本地校正")]
        [STNodeProperty("校正模板", "对 RAW 指针执行的相机校正模板；CVCIE 输入会直接透传", true)]
        public string CalibTempName { get => calibTempName; set { calibTempName = value ?? string.Empty; OnPropertyChanged(); } }

        [Category("本地校正")]
        [STNodeProperty("保存 CVCIE", "默认关闭；关闭时校正结果只保留在流程内存中", true)]
        public bool SaveFiles { get => saveFiles; set { saveFiles = value; OnPropertyChanged(); } }

        protected LocalCalibrationNodeBase(string title, string nodeType, string operatorName, int timeoutMs, params string[] inputNames)
            : base(title, nodeType, operatorName, timeoutMs, inputNames)
        {
        }

        private protected LocalCalibrationExecution ExecuteCalibration(CVStartCFC action)
        {
            LocalFlowFrame sourceFrame;
            bool ownsSourceFrame;
            bool loadedFromFile;
            if (action.TryGetCurrentFrame(out LocalFlowFrame? currentFrame) && currentFrame != null)
            {
                sourceFrame = currentFrame;
                ownsSourceFrame = false;
                loadedFromFile = false;
            }
            else
            {
                if (!SupportsFileFallback)
                {
                    throw new InvalidOperationException("流程中没有可用的本地图像内存帧。");
                }
                string sourceFilePath = ResolveSourceFilePath(action, SourceImageFilePath);
                if (string.IsNullOrWhiteSpace(sourceFilePath))
                {
                    throw new InvalidOperationException("IN_IMG 没有本地图像内存帧，也没有可读取的图像结果，请配置备用图像文件。");
                }
                sourceFrame = LocalFrameFileService.Load(sourceFilePath);
                ownsSourceFrame = true;
                loadedFromFile = true;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            LocalFlowFrame? outputFrame = null;
            bool ownsOutputFrame = false;
            CalibrationParam? calibration = null;
            string sourceFrameId = sourceFrame.FrameId.ToString("N");
            bool calibrated = false;
            try
            {
                using (LocalFlowFrameLease source = sourceFrame.Acquire())
                {
                    if (source.HasRaw)
                    {
                        DeviceCamera device = ResolveDevice(source.Metadata.DeviceCode);
                        calibration = ResolveCalibration(device);
                        if (!device.TryGetCalibrationTemplateFiles(calibration, out IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles, out string? errorMessage))
                        {
                            throw new InvalidOperationException(errorMessage ?? "校正模板无效。");
                        }
                        outputFrame = LocalFrameCalibrationService.Calibrate(source, calibrationFiles, calibration.Name);
                        ownsOutputFrame = true;
                        calibrated = true;
                        if (SaveFiles)
                        {
                            LocalFrameFileService.SaveCapture(outputFrame, device.Config.FileServerCfg.DataBasePath, device.Code);
                        }
                    }
                    else if (source.HasCie)
                    {
                        outputFrame = sourceFrame;
                        ownsOutputFrame = ownsSourceFrame;
                        ownsSourceFrame = false;
                        if (SaveFiles && string.IsNullOrWhiteSpace(outputFrame.CvCieFilePath))
                        {
                            DeviceCamera device = ResolveDevice(source.Metadata.DeviceCode);
                            LocalFrameFileService.SaveCapture(outputFrame, device.Config.FileServerCfg.DataBasePath, device.Code);
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException("当前本地帧既没有 RAW 内存，也没有 CIE 内存。");
                    }
                }

                stopwatch.Stop();
                LocalFlowFrame completedFrame = outputFrame ?? throw new InvalidOperationException("本地校正没有生成输出帧。");
                ownsOutputFrame = false;
                return new LocalCalibrationExecution(
                    completedFrame,
                    completedFrame != sourceFrame || loadedFromFile,
                    calibration,
                    sourceFrameId,
                    loadedFromFile,
                    calibrated,
                    checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue)));
            }
            catch
            {
                if (ownsOutputFrame) outputFrame?.Dispose();
                throw;
            }
            finally
            {
                if (ownsSourceFrame) sourceFrame.Dispose();
            }
        }

        private protected int SaveCalibrationResult(CVStartCFC action, LocalCalibrationExecution execution)
        {
            LocalFlowFrame frame = execution.Frame;
            MeasureBatchModel batch = BatchResultMasterDao.Instance.GetByNameOrCode(action.SerialNumber)
                ?? throw new InvalidOperationException($"找不到流程批次：{action.SerialNumber}");
            string? cieFilePath = NullIfEmpty(frame.CvCieFilePath);
            if (SaveFiles && cieFilePath == null)
            {
                throw new InvalidOperationException("已启用“保存 CVCIE”，但本地校正没有生成 CVCIE 文件。");
            }

            MeasureResultImgModel model = new()
            {
                BatchId = batch.Id,
                ZIndex = ZIndex,
                NDPort = -1,
                Params = JsonConvert.SerializeObject(new
                {
                    SourceFrameId = execution.SourceFrameId,
                    SourceFile = NullIfEmpty(frame.Metadata.SourceFilePath),
                    execution.LoadedFromFile,
                    execution.Calibrated,
                    MemoryOnly = string.IsNullOrWhiteSpace(frame.CvCieFilePath),
                    frame.Metadata.Width,
                    frame.Metadata.Height,
                    frame.Metadata.SourceBpp,
                    frame.Metadata.CieBpp,
                    frame.Metadata.Channels,
                    frame.Metadata.Gain,
                    Exposure = frame.Metadata.Exposure,
                    Calibration = new { ID = execution.Calibration?.Id ?? -1, Name = execution.Calibration?.Name ?? frame.Metadata.CalibrationTemplate }
                }),
                RawFile = cieFilePath == null ? null : Path.GetFileName(cieFilePath),
                FileUrl = cieFilePath,
                FileType = cieFilePath == null ? null : (sbyte)CameraFileType.CIEFile,
                ImgFrameInfo = JsonConvert.SerializeObject(new
                {
                    bpp = frame.Metadata.CieBpp,
                    width = frame.Metadata.Width,
                    height = frame.Metadata.Height,
                    channels = frame.Metadata.Channels
                }),
                ResultCode = 0,
                Result = "ok",
                TotalTime = execution.TotalTime,
                DeviceCode = ResolveDeviceCode(frame.Metadata.DeviceCode),
                CreateDate = DateTime.Now
            };
            int masterId = MeasureImgResultDao.Instance.SaveAndReturnId(model);
            if (masterId <= 0) throw new InvalidOperationException("保存本地校正图像结果失败。");
            return masterId;
        }

        protected override string BuildRunPayload(CVStartCFC action)
        {
            return JsonConvert.SerializeObject(new
            {
                ServiceName = NodeName,
                DeviceCode,
                EventName = operatorCode,
                action.SerialNumber,
                CalibTempName,
                SaveFiles,
                InputMode = "CurrentFrame"
            });
        }

        private protected virtual bool SupportsFileFallback => false;

        private protected virtual string SourceImageFilePath => string.Empty;

        private DeviceCamera ResolveDevice(string frameDeviceCode)
        {
            string deviceCode = ResolveDeviceCode(frameDeviceCode);
            if (string.IsNullOrWhiteSpace(deviceCode)) throw new InvalidOperationException("校正 RAW 内存前必须选择本地相机设备。");
            return ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>()
                .FirstOrDefault(camera => string.Equals(camera.Code, deviceCode, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"找不到本地相机设备：{deviceCode}");
        }

        private CalibrationParam ResolveCalibration(DeviceCamera device)
        {
            if (string.IsNullOrWhiteSpace(CalibTempName)) throw new InvalidOperationException("请选择校正模板。");
            return device.PhyCamera?.CalibrationParams.FirstOrDefault(item => string.Equals(item.Key, CalibTempName, StringComparison.Ordinal))?.Value
                ?? throw new InvalidOperationException($"找不到校正模板：{CalibTempName}");
        }

        private string ResolveDeviceCode(string frameDeviceCode)
            => string.IsNullOrWhiteSpace(DeviceCode) ? frameDeviceCode : DeviceCode;

        private string ResolveSourceFilePath(CVStartCFC action, string imageFilePath)
        {
            if (!string.IsNullOrWhiteSpace(imageFilePath))
            {
                return Path.GetFullPath(imageFilePath.Trim());
            }
            if (!TryGetInputMasterResult(action, 0, out int masterId, out int masterResultType, out _) || masterId <= 0)
            {
                return string.Empty;
            }
            if (masterResultType is not (int)CVCommCore.CVResultType.Camera_Img
                and not (int)CVCommCore.CVResultType.Algorithm_Calibration)
            {
                throw new InvalidOperationException($"IN_IMG 接收到的不是图像结果：MasterId={masterId}，ResultType={masterResultType}。请将图像节点连接到 IN_IMG。");
            }

            MeasureResultImgModel? imageResult = MeasureImgResultDao.Instance.GetById(masterId);
            if (imageResult == null) return string.Empty;
            string? firstCandidate = null;
            foreach (string? candidate in new[] { imageResult.RawFile, imageResult.FileUrl })
            {
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                firstCandidate ??= candidate;
                string fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath)) return fullPath;
            }
            return string.IsNullOrWhiteSpace(firstCandidate) ? string.Empty : Path.GetFullPath(firstCandidate);
        }

        protected static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }

    [STNode("Flow_CustomNodes", "本地校正")]
    [FlowNodePropertyEditorAttribute(nameof(CalibTempName), typeof(FlowCalibrationTemplateEditor))]
    public sealed class LocalCalibrationNode : LocalCalibrationNodeBase
    {
        public LocalCalibrationNode() : base("本地校正", "LocalCalibration", "Calibration", 120000)
        {
        }

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            using LocalCalibrationExecution execution = ExecuteCalibration(action);
            int masterId = SaveCalibrationResult(action, execution);
            execution.Frame.MasterId = masterId;
            action.MasterValue(null, masterId, (int)ViewResultAlgType.Calibration);
            execution.TransferFrameTo(action);
            return new LocalNodeExecutionResult
            {
                Data = new LocalCalibrationNodeResultData
                {
                    FrameId = execution.Frame.FrameId.ToString("N"),
                    MasterId = masterId,
                    TotalTime = execution.TotalTime,
                    Calibrated = execution.Calibrated,
                    HasCieFile = !string.IsNullOrWhiteSpace(execution.Frame.CvCieFilePath),
                    CvCieFilePath = NullIfEmpty(execution.Frame.CvCieFilePath)
                }
            };
        }
    }

}
