using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.POIFilters;
using ColorVision.Engine.Templates.POI.POIRevise;
using FlowEngineLib.Base;
using FlowEngineLib.PropertyEditor;
using ST.Library.UI.NodeEditor;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace ColorVision.Engine.Templates.Flow.Nodes
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

    [STNode("Flow_CustomNodes", "本地 POI")]
    [FlowNodePropertyEditorAttribute(nameof(POITempName), typeof(FlowPoiTemplateEditor))]
    [FlowNodePropertyEditorAttribute(nameof(POIFilterTempName), typeof(FlowPoiFilterTemplateEditor))]
    [FlowNodePropertyEditorAttribute(nameof(POIReviseTempName), typeof(FlowPoiReviseTemplateEditor))]
    public sealed class LocalPoiNode : LocalFlowNodeBase
    {
        private string _POITempName = string.Empty;
        private string _POIFilterTempName = string.Empty;
        private string _POIReviseTempName = string.Empty;

        [Category("本地 POI")]
        [STNodeProperty("POI 模板", "要计算的 POI 模板", true)]
        public string POITempName { get => _POITempName; set { _POITempName = value ?? string.Empty; OnPropertyChanged(); } }

        [Category("本地 POI")]
        [STNodeProperty("POI 过滤", "可选的 POI 过滤模板", true)]
        public string POIFilterTempName { get => _POIFilterTempName; set { _POIFilterTempName = value ?? string.Empty; OnPropertyChanged(); } }

        [Category("本地 POI")]
        [STNodeProperty("POI 修正", "可选的 POI 修正模板", true)]
        public string POIReviseTempName { get => _POIReviseTempName; set { _POIReviseTempName = value ?? string.Empty; OnPropertyChanged(); } }

        public LocalPoiNode() : base("本地 POI", "POI", "Calculate", 60000)
        {
        }

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            if (string.IsNullOrWhiteSpace(POITempName)) throw new InvalidOperationException("请选择 POI 模板。");
            PoiParam poi = TemplatePoi.Params.FirstOrDefault(item => string.Equals(item.Key, POITempName, StringComparison.Ordinal))?.Value
                ?? throw new InvalidOperationException($"找不到 POI 模板：{POITempName}");
            PoiFilterParam? filter = string.IsNullOrWhiteSpace(POIFilterTempName)
                ? null
                : TemplatePoiFilterParam.Params.FirstOrDefault(item => string.Equals(item.Key, POIFilterTempName, StringComparison.Ordinal))?.Value
                    ?? throw new InvalidOperationException($"找不到 POI 过滤模板：{POIFilterTempName}");
            PoiReviseParam? revise = string.IsNullOrWhiteSpace(POIReviseTempName)
                ? null
                : TemplatePoiReviseParam.Params.FirstOrDefault(item => string.Equals(item.Key, POIReviseTempName, StringComparison.Ordinal))?.Value
                    ?? throw new InvalidOperationException($"找不到 POI 修正模板：{POIReviseTempName}");

            if (!action.TryGetCurrentFrame(out LocalFlowFrame? currentFrame) || currentFrame == null)
            {
                throw new InvalidOperationException("流程中没有可用的本地图像内存帧。");
            }
            using (LocalFlowFrameLease frame = currentFrame.Acquire())
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                LocalPoiResultSet result = LocalPoiCalculator.Calculate(frame, poi, filter, revise);
                stopwatch.Stop();
                int totalTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
                ViewResultAlgType resultType = LocalPoiCalculator.ResolveResultType(frame.Metadata.Channels);
                int masterId = LocalFlowResultPersistence.SaveAlgorithmResult(
                    action,
                    resultType,
                    poi.Id,
                    poi.Name,
                    currentFrame.CvCieFilePath,
                    string.IsNullOrWhiteSpace(DeviceCode) ? frame.Metadata.DeviceCode : DeviceCode,
                    ZIndex,
                    totalTime,
                    new
                    {
                        CieMasterId = frame.MasterId,
                        POITemplate = poi.Name,
                        POIFilterTemplate = filter?.Name,
                        POIReviseTemplate = revise?.Name,
                        MemoryOnly = string.IsNullOrWhiteSpace(currentFrame.CvCieFilePath)
                    });
                try
                {
                    LocalPoiCalculator.SaveDetails(masterId, result);
                    action.RuntimeResources.Set(LocalFlowFrameRuntime.GetPoiResultResourceKey(frame.FrameId), result);
                    action.Data["LocalPoiCount"] = result.Points.Count;
                    action.MasterValue(null, masterId, (int)resultType);
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
    }
}
