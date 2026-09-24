using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.POI;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Nodes
{
    internal sealed class LocalPoiNodeResultData
    {
        public string FrameId { get; init; } = string.Empty;
        public string TemplateName { get; init; } = string.Empty;
        public int MasterId { get; init; }
        public int MasterResultType { get; init; } = (int)ViewResultAlgType.POI_XYZ;
        public int PointCount { get; init; }
        public int TotalTime { get; init; }
        public object? POIResult { get; init; }
    }

    [STNode("Flow_CustomNodes", "POI")]
    public sealed class LocalPoiNode : LocalFlowNodeBase
    {
        private string _POITempName = string.Empty;
        private string _POIFilterTempName = string.Empty;
        private string _POIReviseTempName = string.Empty;

        [Category("本地 POI")]
        [STNodeProperty("POI 模板", "要计算的 POI 模板", true)]
        [PropertyEditorType(typeof(PoiTemplatePropertiesEditor))]
        public string POITempName { get => _POITempName; set { _POITempName = value ?? string.Empty; OnPropertyChanged(); } }

        [Browsable(false)]
        public string POIFilterTempName { get => _POIFilterTempName; set { _POIFilterTempName = value ?? string.Empty; OnPropertyChanged(); } }

        [Browsable(false)]
        public string POIReviseTempName { get => _POIReviseTempName; set { _POIReviseTempName = value ?? string.Empty; OnPropertyChanged(); } }

        public LocalPoiNode() : base("POI", "POI", "Calculate")
        {
        }

        protected override string GetCompactSummaryValue() => CompactValueOrDash(POITempName);

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            if (string.IsNullOrWhiteSpace(POITempName)) throw new InvalidOperationException("请选择 POI 模板。");
            PoiParam poi = TemplatePoi.Params.FirstOrDefault(item => string.Equals(item.Key, POITempName, StringComparison.Ordinal))?.Value
                ?? throw new InvalidOperationException($"找不到 POI 模板：{POITempName}");

            bool loadedFromFile = !action.TryGetCurrentFrame(out LocalFlowFrame? currentFrame) || currentFrame == null;
            if (loadedFromFile)
            {
                string path = ResolveInputImageFilePath(action, 0, string.Empty);
                if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("流程中没有可用的本地图像帧或 CVRAW/CVCIE 图像结果。");
                currentFrame = FlowNodeTiming.Run("OpenImage", () => LocalFrameFileService.Load(path));
                _ = TryGetInputMasterResult(action, 0, out int imageMasterId, out _, out _);
                currentFrame.MasterId = imageMasterId;
            }
            try
            {
                string? imagePath = string.IsNullOrWhiteSpace(currentFrame!.CvCieFilePath) ? currentFrame.CvRawFilePath : currentFrame.CvCieFilePath;
                Stopwatch stopwatch = Stopwatch.StartNew();
                using (LocalFlowFrameLease frame = currentFrame.Acquire())
                {
                    LocalPoiResultSet result = LocalPoiCalculator.Calculate(frame, poi);
                    stopwatch.Stop();
                    int totalTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
                    ViewResultAlgType resultType = LocalPoiCalculator.ResolveResultType(frame.Metadata.Channels);
                    int masterId = LocalFlowResultPersistence.SaveAlgorithmResult(
                        action,
                        resultType,
                        poi.Id,
                        poi.Name,
                        imagePath,
                        null,
                        ZIndex,
                        totalTime,
                        new
                        {
                            CieMasterId = frame.MasterId,
                            POITemplate = poi.Name,
                            FlipMode = frame.Metadata.FlipMode.ToString(),
                            FlipApplied = frame.IsFlipApplied,
                            LoadedFromFile = loadedFromFile,
                            InputBuffer = frame.HasCie ? "CIE" : "RAW",
                            MemoryOnly = string.IsNullOrWhiteSpace(imagePath)
                        });
                    try
                    {
                        LocalPoiCalculator.SaveDetails(masterId, result);
                        action.RuntimeResources.Set(LocalFlowFrameRuntime.GetPoiResultResourceKey(frame.FrameId), result);
                        action.Data["LocalPoiCount"] = result.Points.Count;
                        action.MasterValue(null, masterId, (int)resultType);
                        ResultMessageBus.Default.PublishPersisted(ResultRoutes.LocalFlow, ResultKinds.Algorithm, string.Empty, OperatorCode, action.SerialNumber, NodeID, ZIndex, masterId, (int)resultType);
                        action.SetCurrentFrame(currentFrame);
                        loadedFromFile = false;
                        return new LocalNodeExecutionResult
                        {
                            Data = new LocalPoiNodeResultData
                            {
                                FrameId = result.FrameId,
                                TemplateName = result.TemplateName,
                                MasterId = masterId,
                                MasterResultType = (int)resultType,
                                PointCount = result.Points.Count,
                                TotalTime = totalTime,
                                POIResult = result.Points
                            }
                        };
                    }
                    catch
                    {
                        LocalPoiCalculator.DeleteDetails(masterId);
                        LocalFlowResultPersistence.DeleteAlgorithmResult(masterId);
                        throw;
                    }
                }
            }
            finally
            {
                if (loadedFromFile) currentFrame?.Dispose();
            }
        }
    }
}
