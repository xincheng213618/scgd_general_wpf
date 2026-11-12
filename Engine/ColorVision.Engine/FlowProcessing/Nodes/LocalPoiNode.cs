using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Services.POI;
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
        public bool MemoryOnly { get; init; }
        public bool DatabasePersisted { get; init; } = true;
        public string? ResultHandle { get; init; }
        public string? ResultCsvFile { get; init; }
    }

    [STNode("Flow_LocalDevices", "POI", CategoryOrder = 9900, NodeOrder = 50)]
    public sealed class LocalPoiNode : LocalFlowNodeBase
    {
        private string _POITempName = string.Empty;
        private string _POIFilterTempName = string.Empty;
        private string _POIReviseTempName = string.Empty;

        [Category("本地 POI")]
        [STNodeProperty("POI 模板", "要计算的 POI 模板；上游为本地灯珠点集时使用该点集，无需选择模板且测量明细不入库。", true)]
        [PropertyEditorType(typeof(PoiTemplatePropertiesEditor))]
        public string POITempName { get => _POITempName; set { _POITempName = value ?? string.Empty; OnPropertyChanged(); } }

        [Browsable(false)]
        public string POIFilterTempName { get => _POIFilterTempName; set { _POIFilterTempName = value ?? string.Empty; OnPropertyChanged(); } }

        [Browsable(false)]
        public string POIReviseTempName { get => _POIReviseTempName; set { _POIReviseTempName = value ?? string.Empty; OnPropertyChanged(); } }

        private string resultDirectory = string.Empty;
        [Category("本地 POI"), STNodeProperty("本地结果目录", "仅内存测量时可选的 CSV 输出目录（绝对路径）；留空不写文件，不写数据库。", true)]
        public string ResultDirectory { get => resultDirectory; set { resultDirectory = value ?? string.Empty; OnPropertyChanged(); } }

        public LocalPoiNode() : base("POI", "POI", "Calculate")
        {
        }

        protected override string GetCompactSummaryValue() => CompactValueOrDash(POITempName);

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            CVStartCFC input = GetInputActionSnapshot(action, 0) ?? action;
            input.Data.TryGetValue("MasterValue", out object? inputValue);
            _ = LocalFlowPoiPointSet.TryResolve(input, inputValue?.ToString(), out LocalFlowPoiPointSet? pointSet);
            PoiParam? poi = null;
            if (pointSet == null)
            {
                if (string.IsNullOrWhiteSpace(POITempName)) throw new InvalidOperationException("请选择 POI 模板，或连接本地灯珠定位节点。");
                poi = TemplatePoi.Params.FirstOrDefault(item => string.Equals(item.Key, POITempName, StringComparison.Ordinal))?.Value
                    ?? throw new InvalidOperationException($"找不到 POI 模板：{POITempName}");
            }

            bool loadedFromFile = !action.TryGetCurrentFrame(out LocalFlowFrame? currentFrame) || currentFrame == null;
            if (loadedFromFile)
            {
                string path = ResolveInputImageFilePath(action, 0);
                if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("流程中没有可用的本地图像帧或 CVRAW/CVCIE 图像结果。");
                currentFrame = FlowNodeTiming.Run("OpenImage", () => LocalFrameFileService.Load(path));
                _ = TryGetInputMasterResult(action, 0, out int imageMasterId, out _, out _);
                currentFrame.MasterId = imageMasterId;
            }
            try
            {
                poi ??= pointSet!.CreateTemplate(currentFrame!);
                string? imagePath = string.IsNullOrWhiteSpace(currentFrame!.CvCieFilePath) ? currentFrame.CvRawFilePath : currentFrame.CvCieFilePath;
                Stopwatch stopwatch = Stopwatch.StartNew();
                using (LocalFlowFrameLease frame = currentFrame.Acquire())
                {
                    LocalPoiResultSet result = LocalPoiCalculator.Calculate(frame, poi);
                    stopwatch.Stop();
                    int totalTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
                    ViewResultAlgType resultType = LocalPoiCalculator.ResolveResultType(frame.Metadata.Channels);
                    action.RuntimeResources.StopToken.ThrowIfCancellationRequested();
                    if (pointSet != null || !action.PersistResults)
                    {
                        (string handle, string? csvFile) = LocalPoiCalculator.StoreRuntimeResult(action, result, resultType, ResultDirectory);
                        action.SetCurrentFrame(currentFrame);
                        loadedFromFile = false;
                        return new LocalNodeExecutionResult { Data = new LocalPoiNodeResultData
                        {
                            FrameId = result.FrameId, TemplateName = result.TemplateName, MasterId = 0,
                            MasterResultType = (int)resultType, PointCount = result.Points.Count, TotalTime = totalTime,
                            MemoryOnly = csvFile == null, DatabasePersisted = false, ResultHandle = handle, ResultCsvFile = csvFile
                        } };
                    }
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
                        action.SetRuntimeResource(LocalFlowFrameRuntime.GetPoiResultResourceKey(frame.FrameId), result);
                        action.SetRuntimeResource(LocalPoiCalculator.ResultResourceKey(masterId), result);
                        action.Data["LocalPoiCount"] = result.Points.Count;
                        action.MasterValue(null, masterId, (int)resultType);
                        ResultMessageBus.Default.PublishPersisted(ResultRoutes.LocalFlow, ResultKinds.Algorithm, string.Empty, OperatorCode, action.SerialNumber, ExecutionNodeId, ZIndex, masterId, (int)resultType);
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
