using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.POI;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Nodes
{
    internal sealed class LocalCalibrationRealPoiNodeResultData : LocalCalibrationNodeResultData
    {
        public int CalibrationMasterId { get; init; }
        public int PoiMasterId { get; init; }
        public string PoiTemplateName { get; init; } = string.Empty;
        public int PointCount { get; init; }
        public bool LoadedFromFile { get; init; }
        public bool NinePointCalibrationEnabled { get; init; }
        public int NinePointCalibrationCorrectedCount { get; init; }
        public IReadOnlyList<NinePointBlockCalibrationPoint>? NinePointCalibration { get; init; }
        public object? POIResult { get; init; }
    }

    [STNode("Flow_LocalDevices", "校正+实时 POI", CategoryOrder = 9900, NodeOrder = 70)]
    [FlowEngineLib.FlowNodeDocumentation("使用上游图像和布点结果测量实时 POI。",
        Usage = "图像接 IN_IMG，布点结果接 IN_POI。相机已传入内存色度，或 CVRAW 附带可回放色度参数时，清空校正模板即可直接测量。")]
    public sealed class LocalCalibrationRealPoiNode : LocalCalibrationNodeBase
    {
        private static readonly string[] InputPortNames = { "IN_IMG", "IN_POI" };
        private bool enableNinePointCalibration;

        [Category("本地校正")]
        [DefaultValue(false)]
        [STNodeProperty("9点校准", "仅在关注点恰好为9个时，在当前校正图像中寻找对应亮块中心。找不到的点保留原坐标；其他点数自动跳过。", true)]
        public bool EnableNinePointCalibration
        {
            get => enableNinePointCalibration;
            set { enableNinePointCalibration = value; OnPropertyChanged(); }
        }

        public LocalCalibrationRealPoiNode() : base("校正+实时 POI", "LocalCalibrationRealPOI", "Real_POI", InputPortNames)
        {
            AllowAcceleration = true;
        }

        private protected override void ValidateImageInputPath(string filePath)
        {
            if (string.Equals(Path.GetExtension(filePath), ".cvcie", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("校正+实时 POI 的 IN_IMG 不接受 .cvcie 文件。请接入相机取图节点，或加载带可回放色度校正参数的 CVRAW。");
        }

        private protected override float[]? ResolveZeroExposureFallback(CVStartCFC action, LocalFlowFrame sourceFrame)
        {
            if (sourceFrame.Metadata.Exposure.Length == 0 || sourceFrame.Metadata.Exposure.Any(value => value != 0)) return null;
            if (!TryGetInputMasterResult(action, 0, out int imageMasterId, out _, out _) || imageMasterId <= 0) return null;

            MeasureResultImgModel? imageResult = MeasureImgResultDao.Instance.GetById(imageMasterId);
            return ReadExposureFromImageResultParams(imageResult?.Params);
        }

        internal static float[]? ReadExposureFromImageResultParams(string? parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters)) return null;
            try
            {
                if (JToken.Parse(parameters) is not JObject root) return null;
                JToken? exposureToken = root.GetValue("ExpTime", StringComparison.OrdinalIgnoreCase)
                    ?? root.GetValue("Exposure", StringComparison.OrdinalIgnoreCase);
                if (exposureToken == null) return null;

                IEnumerable<JToken> values = exposureToken is JArray array ? array.Children() : new[] { exposureToken };
                List<float> exposure = new();
                foreach (JToken valueToken in values)
                {
                    if (!float.TryParse(valueToken.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                        || !float.IsFinite(value)
                        || value <= 0)
                    {
                        return null;
                    }
                    exposure.Add(value);
                }
                return exposure.Count == 0 ? null : exposure.ToArray();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            _ = TryGetInputMasterResult(action, 1, out int poiInputMasterId, out int poiInputResultType, out _);
            LocalRealPoiParameters parameters = LocalRealPoiInputResolver.Resolve(poiInputMasterId, poiInputResultType, InputPortNames[0]);
            bool useInputColor = string.IsNullOrWhiteSpace(CalibTempName);
            using LocalCalibrationExecution execution = ExecuteCalibration(action, useInputColor);
            if (!execution.Frame.HasCie && execution.Frame.ColorCalibration == null)
            {
                throw new InvalidOperationException("实时 POI 需要 CIE 或带色度校正参数的 RAW，请在校正模板中选择一个亮度或颜色校正文件。");
            }
            int inputImageMasterId = execution.Frame.MasterId;
            int calibrationMasterId = -1;
            int poiMasterId = -1;
            try
            {
                MeasureResultImgModel? persistedCalibration = null;
                if (!useInputColor)
                {
                    persistedCalibration = SaveCalibrationResult(action, execution);
                    calibrationMasterId = persistedCalibration.Id;
                    execution.Frame.MasterId = calibrationMasterId;
                }

                Stopwatch stopwatch = Stopwatch.StartNew();
                LocalPoiResultSet result;
                IReadOnlyList<NinePointBlockCalibrationPoint>? calibrationPoints = null;
                using (LocalFlowFrameLease frame = execution.Frame.Acquire())
                {
                    PoiParam poi = LocalNinePointPoiCalibration.ApplyIfEnabled(EnableNinePointCalibration, parameters.Poi, seeds =>
                    {
                        HImage image = LocalFindLuminousAreaNode.CreateBorrowedImage(frame);
                        calibrationPoints = FlowNodeTiming.Run("NinePointCalibration", () =>
                            NinePointBlockCalibrator.Refine(image, seeds, new NinePointBlockCalibrationOptions(), action.RuntimeResources.StopToken));
                        return calibrationPoints;
                    });
                    result = LocalPoiCalculator.Calculate(frame, poi);
                }
                stopwatch.Stop();
                int correctedCount = calibrationPoints?.Count(point => point.Success) ?? 0;
                int poiTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
                ViewResultAlgType resultType = LocalPoiCalculator.ResolveResultType(execution.Frame.Metadata.Channels);
                string algorithmDeviceCode = GetFirstAvailableDeviceCode<DeviceAlgorithm>();
                string? rawFilePath = NullIfEmpty(execution.Frame.CvRawFilePath);
                poiMasterId = LocalFlowResultPersistence.SaveAlgorithmResult(
                    action,
                    resultType,
                    parameters.Poi.Id,
                    parameters.Poi.Name,
                    rawFilePath,
                    algorithmDeviceCode,
                    ZIndex,
                    poiTime,
                    new
                    {
                        CieMasterId = persistedCalibration?.Id ?? inputImageMasterId,
                        POISourceMasterId = parameters.SourceMasterId > 0 ? (int?)parameters.SourceMasterId : null,
                        CalibrationTemplate = execution.Calibration?.Name ?? execution.Frame.Metadata.CalibrationTemplate,
                        POITemplate = parameters.Poi.Name,
                        AllowAcceleration,
                        NinePointCalibration = new { Enabled = EnableNinePointCalibration, Attempted = calibrationPoints != null, CorrectedCount = correctedCount, Points = calibrationPoints },
                        MemoryOnly = rawFilePath == null
                    });
                LocalPoiCalculator.SaveDetails(poiMasterId, result);

                action.SetRuntimeResource(LocalFlowFrameRuntime.GetPoiResultResourceKey(execution.Frame.FrameId), result);
                action.SetRuntimeResource(LocalPoiCalculator.ResultResourceKey(poiMasterId), result);
                action.Data["LocalPoiCount"] = result.Points.Count;
                action.Data["LocalCalibrationMasterId"] = calibrationMasterId;
                action.MasterValue(null, poiMasterId, (int)resultType);
                execution.TransferFrameTo(action);
                if (persistedCalibration != null)
                {
                    (string calibrationRoute, string calibrationDeviceCode) = ResolveCalibrationResultTarget(persistedCalibration.DeviceCode ?? string.Empty);
                    ResultMessageBus.Default.PublishPersisted(calibrationRoute, ResultKinds.Image, calibrationDeviceCode, OperatorCode, action.SerialNumber, NodeID, ZIndex, calibrationMasterId, (int)ViewResultAlgType.Calibration);
                }
                ResultMessageBus.Default.PublishPersisted(ResultRoutes.Algorithm, ResultKinds.Algorithm, algorithmDeviceCode, OperatorCode, action.SerialNumber, NodeID, ZIndex, poiMasterId, (int)resultType);
                return new LocalNodeExecutionResult
                {
                    Data = new LocalCalibrationRealPoiNodeResultData
                    {
                        FrameId = execution.Frame.FrameId.ToString("N"),
                        MasterId = poiMasterId,
                        MasterResultType = (int)resultType,
                        CalibrationMasterId = calibrationMasterId,
                        PoiMasterId = poiMasterId,
                        TotalTime = execution.TotalTime + poiTime,
                        FlipMode = execution.Frame.Metadata.FlipMode.ToString(),
                        FlipApplied = execution.Frame.IsFlipApplied,
                        LoadedFromFile = execution.LoadedFromFile,
                        Calibrated = execution.Calibrated,
                        HasRaw = execution.Frame.HasRaw,
                        HasCie = execution.Frame.HasCie,
                        CvRawFilePath = rawFilePath,
                        PoiTemplateName = result.TemplateName,
                        PointCount = result.Points.Count,
                        NinePointCalibrationEnabled = EnableNinePointCalibration,
                        NinePointCalibrationCorrectedCount = correctedCount,
                        NinePointCalibration = calibrationPoints,
                        POIResult = result.Points
                    }
                };
            }
            catch
            {
                LocalPoiCalculator.DeleteDetails(poiMasterId);
                LocalFlowResultPersistence.DeleteAlgorithmResult(poiMasterId);
                throw;
            }
        }

        protected override string BuildRunPayload(CVStartCFC action)
        {
            return JsonConvert.SerializeObject(new
            {
                ServiceName = NodeName,
                DeviceCode,
                EventName = OperatorCode,
                action.SerialNumber,
                CalibTempName,
                AllowAcceleration,
                EnableNinePointCalibration,
                InputPriority = "CurrentFrameThenInputFile",
                InputPorts = InputPortNames
            });
        }
    }
}
