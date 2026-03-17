using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.POIFilters;
using ColorVision.Engine.Templates.POI.POIOutput;
using ColorVision.Engine.Templates.POI.POIGenCali;
using ColorVision.Engine.Templates.POI.POIRevise;
using ColorVision.Engine.Templates.POI.BuildPoi;
using ColorVision.Engine.Templates.Jsons.BuildPOIAA;
using ColorVision.Engine.Templates.Jsons.PoiAnalysis;
using System.Linq;

namespace ColorVision.Engine.Templates.Flow.NodeConfigurator
{
    [NodeConfigurator(typeof(FlowEngineLib.Node.POI.POIReviseNode))]
    public class POIReviseNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.POI.POIReviseNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddTemplatePanel(name => node.TemplateName = name, node.TemplateName, "POI修正标定", new TemplatePoiGenCalParam());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.POI.RealPOINode))]
    public class RealPOINodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.POI.RealPOINode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddTemplatePanel(name => node.FilterTemplateName = name, node.FilterTemplateName, "POI过滤", new TemplatePoiFilterParam());
            context.AddTemplatePanel(name => node.ReviseTemplateName = name, node.ReviseTemplateName, "POI修正", new TemplatePoiReviseParam());
            context.AddTemplatePanel(name => node.OutputTemplateName = name, node.OutputTemplateName, "文件输出模板", new TemplatePoiOutputParam());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.POINode))]
    public class POINodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.POINode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddImagePath(name => node.ImgFileName = name, node.ImgFileName);
            context.AddTemplatePanel(name => node.TempName = name, node.TempName, "POI模板", new TemplatePoi());
            context.AddTemplatePanel(name => node.FilterTemplateName = name, node.FilterTemplateName, "POI过滤", new TemplatePoiFilterParam());
            context.AddTemplatePanel(name => node.ReviseTemplateName = name, node.ReviseTemplateName, "POI修正", new TemplatePoiReviseParam());
            context.AddTemplatePanel(name => node.OutputTemplateName = name, node.OutputTemplateName, "文件输出模板", new TemplatePoiOutputParam());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.BuildPOINode))]
    public class BuildPOINodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.BuildPOINode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddImagePath(name => node.ImgFileName = name, node.ImgFileName);
            context.AddTemplatePanel(name => node.TemplateName = name, node.TemplateName, "布点模板", new TemplateBuildPoi());
            context.AddTemplateJsonPanel(name => node.TemplateName = name, node.TemplateName, "ABuildPOIAAA", new TemplateBuildPOIAA());
            context.AddTemplatePanel(name => node.RePOITemplateName = name, node.RePOITemplateName, "RePOI", new TemplatePoi());
            context.AddTemplatePanel(name => node.LayoutROITemplate = name, node.LayoutROITemplate, "布点ROI", new TemplatePoi());
            context.AddTemplatePanel(name => node.SavePOITempName = name, node.SavePOITempName, "SavePOI", new TemplatePoi());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.POI.POIAnalysisNode))]
    public class POIAnalysisNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.POI.POIAnalysisNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "PoiAnalysis", new TemplatePoiAnalysis());
        }
    }
}
