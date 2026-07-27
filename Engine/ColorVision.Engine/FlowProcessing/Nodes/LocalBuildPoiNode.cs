using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates.FindLightArea;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.BuildPoi;
using FlowEngineLib.Base;
using FlowEngineLib.PropertyEditor;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Nodes
{
    internal sealed class LocalBuildPoiNodeResultData
    {
        public int MasterId { get; init; }
        public int MasterResultType { get; init; } = (int)ViewResultAlgType.BuildPOI;
        public int SourceMasterId { get; init; }
        public int SourceResultType { get; init; }
        public string TemplateName { get; init; } = string.Empty;
        public string? LayoutTemplateName { get; init; }
        public int PointCount { get; init; }
        public int TotalTime { get; init; }
    }

    [STNode("Flow_CustomNodes", "本地关注点布点(Re)")]
    [FlowNodePropertyEditorAttribute(nameof(LayoutROITemplateName), typeof(FlowPoiTemplateEditor))]
    [FlowNodePropertyEditorAttribute(nameof(RePOITemplateName), typeof(FlowPoiTemplateEditor))]
    public sealed class LocalBuildPoiNode : LocalFlowNodeBase
    {
        private string layoutRoiTemplateName = "POI_W_AUTO";
        private string rePoiTemplateName = "POI_Black";
        private string prefixName = string.Empty;

        [Category("本地关注点布点(Re)")]
        [STNodeProperty("布点 ROI", "包含目标四角点的 POI 模板，例如 POI_W_AUTO", true)]
        public string LayoutROITemplateName
        {
            get => layoutRoiTemplateName;
            set
            {
                layoutRoiTemplateName = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        [Category("本地关注点布点(Re)")]
        [STNodeProperty("POI 模板(Re)", "包含画布四角参考点、用于 ReMapping 的 POI 模板", true)]
        public string RePOITemplateName
        {
            get => rePoiTemplateName;
            set
            {
                rePoiTemplateName = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        [Category("本地关注点布点(Re)")]
        [STNodeProperty("名称前缀", "添加到映射后每个关注点名称之前的前缀", true)]
        public string PrefixName
        {
            get => prefixName;
            set
            {
                prefixName = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public LocalBuildPoiNode() : base("本地关注点布点(Re)", "LocalBuildPOI", "BuildPOI", 60000)
        {
        }

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            _ = TryGetInputMasterResult(action, 0, out int sourceMasterId, out int sourceResultType, out _);
            PoiParam template = LocalPoiTemplateResolver.ResolvePoiTemplate(RePOITemplateName, "POI 模板(Re)");
            LocalPoiMappingPoint[] layout = ResolveLayout(sourceMasterId, sourceResultType, out string? layoutTemplateName);

            Stopwatch stopwatch = Stopwatch.StartNew();
            List<LocalPoiRemappedPoint> points = LocalPoiRemappingCalculator.Remap(template, layout, PrefixName);
            stopwatch.Stop();
            int totalTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
            int masterId = LocalFlowResultPersistence.SaveAlgorithmResult(
                action,
                ViewResultAlgType.BuildPOI,
                template.Id,
                template.Name,
                null,
                null,
                ZIndex,
                totalTime,
                new
                {
                    BuildType = "ReMapping",
                    SourceMasterId = sourceMasterId,
                    SourceResultType = sourceResultType,
                    LayoutROITemplate = layoutTemplateName,
                    RePOITemplate = template.Name,
                    PrefixName,
                    PointCount = points.Count,
                    ImageRead = false,
                    MemoryOnly = true
                });
            try
            {
                if (masterId > 0)
                    LocalPoiRemappingCalculator.SaveDetails(masterId, points);
                action.Data["LocalBuildPoiCount"] = points.Count;
                action.Data["LocalBuildPoiSourceMasterId"] = sourceMasterId;
                action.MasterValue(null, masterId, (int)ViewResultAlgType.BuildPOI);
                return new LocalNodeExecutionResult
                {
                    Data = new LocalBuildPoiNodeResultData
                    {
                        MasterId = masterId,
                        SourceMasterId = sourceMasterId,
                        SourceResultType = sourceResultType,
                        TemplateName = template.Name,
                        LayoutTemplateName = layoutTemplateName,
                        PointCount = points.Count,
                        TotalTime = totalTime
                    }
                };
            }
            catch
            {
                LocalPoiRemappingCalculator.DeleteDetails(masterId);
                LocalFlowResultPersistence.DeleteAlgorithmResult(masterId);
                throw;
            }
        }

        protected override string BuildRunPayload(CVStartCFC action)
        {
            return JsonConvert.SerializeObject(new
            {
                ServiceName = NodeName,
                EventName = operatorCode,
                action.SerialNumber,
                BuildType = "ReMapping",
                LayoutROITemplateName,
                RePOITemplateName,
                PrefixName,
                ImageRead = false
            });
        }

        private LocalPoiMappingPoint[] ResolveLayout(int sourceMasterId, int sourceResultType, out string? layoutTemplateName)
        {
            if (!string.IsNullOrWhiteSpace(LayoutROITemplateName))
            {
                PoiParam layoutTemplate = LocalPoiTemplateResolver.ResolvePoiTemplate(LayoutROITemplateName, "布点 ROI");
                if (layoutTemplate.PoiPoints.Count != 4)
                {
                    throw new InvalidOperationException($"布点 ROI 必须包含 4 个角点：{layoutTemplate.Name}，当前为 {layoutTemplate.PoiPoints.Count} 个。");
                }
                layoutTemplateName = layoutTemplate.Name;
                return layoutTemplate.PoiPoints
                    .Select(point => new LocalPoiMappingPoint(point.PixX, point.PixY))
                    .ToArray();
            }

            layoutTemplateName = null;
            if (sourceMasterId <= 0)
            {
                throw new InvalidOperationException("请选择布点 ROI，或连接有效的光区四角结果。");
            }
            if (!IsSupportedSourceResult(sourceResultType))
            {
                throw new InvalidOperationException($"IN 接收到的不是光区四角结果：MasterId={sourceMasterId}，ResultType={sourceResultType}；相机输入请配置布点 ROI。");
            }
            List<AlgResultLightAreaModel> sourcePoints = AlgResultLightAreaDao.Instance.GetAllByPid(sourceMasterId)
                .OrderBy(point => point.Id)
                .ToList();
            if (sourcePoints.Count != 4)
            {
                throw new InvalidOperationException($"光区结果必须包含 4 个角点：MasterId={sourceMasterId}，当前为 {sourcePoints.Count} 个。");
            }
            return sourcePoints
                .Select(point => new LocalPoiMappingPoint(point.PosX, point.PosY))
                .ToArray();
        }

        private static bool IsSupportedSourceResult(int resultType)
        {
            return resultType is (int)ViewResultAlgType.LightArea
                or (int)ViewResultAlgType.FindLightArea
                or (int)ViewResultAlgType.BlackMura_Calc;
        }
    }

    [STNode("Flow_CustomNodes", "本地关注点布点(参数)")]
    [FlowNodePropertyEditorAttribute(nameof(ParameterTemplateName), typeof(FlowBuildPoiTemplateEditor))]
    [FlowNodePropertyEditorAttribute(nameof(LayoutROITemplateName), typeof(FlowPoiTemplateEditor))]
    public sealed class LocalBuildPoiByTemplateNode : LocalFlowNodeBase
    {
        private string parameterTemplateName = string.Empty;
        private string layoutRoiTemplateName = "POI_W_AUTO";

        [Category("本地关注点布点(参数)")]
        [STNodeProperty("参数模板", "使用行列、边距、点类型和点尺寸生成关注点", true)]
        public string ParameterTemplateName
        {
            get => parameterTemplateName;
            set
            {
                parameterTemplateName = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        [Category("本地关注点布点(参数)")]
        [STNodeProperty("布点 ROI", "提供布点区域的 POI 模板，例如 POI_W_AUTO", true)]
        public string LayoutROITemplateName
        {
            get => layoutRoiTemplateName;
            set
            {
                layoutRoiTemplateName = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public LocalBuildPoiByTemplateNode() : base("本地关注点布点(参数)", "LocalBuildPOICommon", "BuildPOI", 60000)
        {
        }

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            ParamBuildPoi parameter = LocalPoiTemplateResolver.ResolveBuildPoiTemplate(ParameterTemplateName);
            PoiParam layoutTemplate = LocalPoiTemplateResolver.ResolvePoiTemplate(LayoutROITemplateName, "布点 ROI");

            Stopwatch stopwatch = Stopwatch.StartNew();
            List<LocalPoiRemappedPoint> points = LocalPoiLayoutCalculator.Build(parameter, layoutTemplate);
            stopwatch.Stop();
            int totalTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
            int masterId = LocalFlowResultPersistence.SaveAlgorithmResult(
                action,
                ViewResultAlgType.BuildPOI,
                parameter.Id,
                parameter.Name,
                null,
                null,
                ZIndex,
                totalTime,
                new
                {
                    BuildType = "Common",
                    ParameterTemplate = parameter.Name,
                    LayoutROITemplate = layoutTemplate.Name,
                    parameter.POILayout,
                    PointCount = points.Count,
                    ImageRead = false,
                    MemoryOnly = true
                });
            try
            {
                if (masterId > 0)
                    LocalPoiRemappingCalculator.SaveDetails(masterId, points);
                action.Data["LocalBuildPoiCount"] = points.Count;
                action.MasterValue(null, masterId, (int)ViewResultAlgType.BuildPOI);
                return new LocalNodeExecutionResult
                {
                    Data = new LocalBuildPoiNodeResultData
                    {
                        MasterId = masterId,
                        TemplateName = parameter.Name,
                        LayoutTemplateName = layoutTemplate.Name,
                        PointCount = points.Count,
                        TotalTime = totalTime
                    }
                };
            }
            catch
            {
                LocalPoiRemappingCalculator.DeleteDetails(masterId);
                LocalFlowResultPersistence.DeleteAlgorithmResult(masterId);
                throw;
            }
        }

        protected override string BuildRunPayload(CVStartCFC action)
        {
            return JsonConvert.SerializeObject(new
            {
                ServiceName = NodeName,
                EventName = operatorCode,
                action.SerialNumber,
                BuildType = "Common",
                ParameterTemplateName,
                LayoutROITemplateName,
                ImageRead = false
            });
        }
    }
}
