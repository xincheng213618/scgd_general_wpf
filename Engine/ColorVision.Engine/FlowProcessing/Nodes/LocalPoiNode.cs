using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.POI;
using FlowEngineLib.Base;
using FlowEngineLib.PropertyEditor;
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

    [STNode("Flow_CustomNodes", "本地 POI")]
    [FlowNodePropertyEditorAttribute(nameof(POITempName), typeof(FlowPoiTemplateEditor))]
    public sealed class LocalPoiNode : LocalFlowNodeBase
    {
        private string _POITempName = string.Empty;
        private string _POIFilterTempName = string.Empty;
        private string _POIReviseTempName = string.Empty;

        [Category("本地 POI")]
        [STNodeProperty("POI 模板", "要计算的 POI 模板", true)]
        public string POITempName { get => _POITempName; set { _POITempName = value ?? string.Empty; OnPropertyChanged(); } }

        [Browsable(false)]
        public string POIFilterTempName { get => _POIFilterTempName; set { _POIFilterTempName = value ?? string.Empty; OnPropertyChanged(); } }

        [Browsable(false)]
        public string POIReviseTempName { get => _POIReviseTempName; set { _POIReviseTempName = value ?? string.Empty; OnPropertyChanged(); } }

        public LocalPoiNode() : base("本地 POI", "POI", "Calculate")
        {
            SelectFirstAvailableDevice<DeviceAlgorithm>();
        }

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            if (string.IsNullOrWhiteSpace(POITempName)) throw new InvalidOperationException("请选择 POI 模板。");
            PoiParam poi = TemplatePoi.Params.FirstOrDefault(item => string.Equals(item.Key, POITempName, StringComparison.Ordinal))?.Value
                ?? throw new InvalidOperationException($"找不到 POI 模板：{POITempName}");

            if (!action.TryGetCurrentFrame(out LocalFlowFrame? currentFrame) || currentFrame == null)
            {
                throw new InvalidOperationException("流程中没有可用的本地图像内存帧。");
            }
            Stopwatch stopwatch = Stopwatch.StartNew();
            using (LocalFlowFrameLease frame = currentFrame.Acquire())
            {
                LocalPoiResultSet result = LocalPoiCalculator.Calculate(frame, poi);
                stopwatch.Stop();
                int totalTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
                ViewResultAlgType resultType = LocalPoiCalculator.ResolveResultType(frame.Metadata.Channels);
                string algorithmDeviceCode = ResolveAvailableDeviceCode<DeviceAlgorithm>();
                int masterId = LocalFlowResultPersistence.SaveAlgorithmResult(
                    action,
                    resultType,
                    poi.Id,
                    poi.Name,
                    currentFrame.CvCieFilePath,
                    algorithmDeviceCode,
                    ZIndex,
                    totalTime,
                    new
                    {
                        CieMasterId = frame.MasterId,
                        POITemplate = poi.Name,
                        FlipMode = frame.Metadata.FlipMode.ToString(),
                        FlipApplied = frame.IsCieFlipApplied,
                        MemoryOnly = string.IsNullOrWhiteSpace(currentFrame.CvCieFilePath)
                    });
                try
                {
                    LocalPoiCalculator.SaveDetails(masterId, result);
                    action.RuntimeResources.Set(LocalFlowFrameRuntime.GetPoiResultResourceKey(frame.FrameId), result);
                    action.Data["LocalPoiCount"] = result.Points.Count;
                    action.MasterValue(null, masterId, (int)resultType);
                    ResultMessageBus.Default.PublishPersisted(ResultRoutes.Algorithm, ResultKinds.Algorithm, algorithmDeviceCode, OperatorCode, action.SerialNumber, NodeID, ZIndex, masterId, (int)resultType);
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
