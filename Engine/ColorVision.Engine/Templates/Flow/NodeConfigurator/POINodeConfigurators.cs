using ColorVision.Engine.Properties;
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
            context.AddTemplatePanel(name => node.TemplateName = name, node.TemplateName, Properties.Resources.POIReviseCalib, new TemplatePoiGenCalParam());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.POI.RealPOINode))]
    public class RealPOINodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.POI.RealPOINode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddTemplatePanel(name => node.FilterTemplateName = name, node.FilterTemplateName, Properties.Resources.POIFilter, new TemplatePoiFilterParam());
            context.AddTemplatePanel(name => node.ReviseTemplateName = name, node.ReviseTemplateName, Properties.Resources.POIRevise, new TemplatePoiReviseParam());
            context.AddTemplatePanel(name => node.OutputTemplateName = name, node.OutputTemplateName, Properties.Resources.FileOutputTemplate, new TemplatePoiOutputParam());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.POINode))]
    public class POINodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.POINode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddTemplatePanel(name => node.TempName = name, node.TempName, Properties.Resources.POITemplate, new TemplatePoi());
            context.AddTemplatePanel(name => node.FilterTemplateName = name, node.FilterTemplateName, Properties.Resources.POIFilter, new TemplatePoiFilterParam());
            context.AddTemplatePanel(name => node.ReviseTemplateName = name, node.ReviseTemplateName, Properties.Resources.POIRevise, new TemplatePoiReviseParam());
            context.AddTemplatePanel(name => node.OutputTemplateName = name, node.OutputTemplateName, Properties.Resources.FileOutputTemplate, new TemplatePoiOutputParam());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.BuildPOINode))]
    public class BuildPOINodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.BuildPOINode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddTemplatePanel(name => node.TemplateName = name, node.TemplateName, Properties.Resources.BuildTemplate, new TemplateBuildPoi());
            context.AddTemplateJsonPanel(name => node.TemplateName = name, node.TemplateName, "ABuildPOIAAA", new TemplateBuildPOIAA());
            context.AddTemplatePanel(name => node.RePOITemplateName = name, node.RePOITemplateName, "RePOI", new TemplatePoi());
            context.AddTemplatePanel(name => node.LayoutROITemplate = name, node.LayoutROITemplate, Properties.Resources.BuildROI, new TemplatePoi());
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
