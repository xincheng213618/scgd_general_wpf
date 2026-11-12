using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Templates.Jsons.AutoExpTime;
using FlowEngineLib;

namespace ColorVision.Engine.FlowProcessing.Editor.NodeConfiguration
{
    [NodeConfigurator(typeof(FlowEngineLib.Node.Camera.CVAOICameraNode))]
    public class CVAOICameraNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Camera.CVAOICameraNode)context.Node;

            context.AddTemplatePanel(nameof(node.TempName), $"{Properties.Resources.ExposureTemplate} V1", new TemplateAutoExpTime());
            context.AddTemplateJsonPanel(nameof(node.TempName), $"{Properties.Resources.ExposureTemplate} V2", new TemplateAutoExpTimeV2());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.AOILocatePixelsCameraNode))]
    public class AOILocatePixelsCameraNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (AOILocatePixelsCameraNode)context.Node;

            context.AddTemplatePanel(nameof(node.AutoExpTempName), $"{Properties.Resources.ExposureTemplate} V1", new TemplateAutoExpTime());
            context.AddTemplateJsonPanel(nameof(node.AutoExpTempName), $"{Properties.Resources.ExposureTemplate} V2", new TemplateAutoExpTimeV2());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.AOIRegisterPixelsCameraNode))]
    public class AOIRegisterPixelsCameraNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (AOIRegisterPixelsCameraNode)context.Node;

            context.AddTemplatePanel(nameof(node.AutoExpTempName), $"{Properties.Resources.ExposureTemplate} V1", new TemplateAutoExpTime());
            context.AddTemplateJsonPanel(nameof(node.AutoExpTempName), $"{Properties.Resources.ExposureTemplate} V2", new TemplateAutoExpTimeV2());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.AOILocAndRegPixelsCameraNode))]
    public class AOILocAndRegPixelsCameraNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (AOILocAndRegPixelsCameraNode)context.Node;

            context.AddTemplatePanel(nameof(node.AutoExpTempName), $"{Properties.Resources.ExposureTemplate} V1", new TemplateAutoExpTime());
            context.AddTemplateJsonPanel(nameof(node.AutoExpTempName), $"{Properties.Resources.ExposureTemplate} V2", new TemplateAutoExpTimeV2());
        }
    }
    

    [NodeConfigurator(typeof(FlowEngineLib.Node.Camera.CVAOI2CameraNode))]
    public class CVAOI2CameraNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Camera.CVAOI2CameraNode)context.Node;

            context.AddTemplatePanel(nameof(node.TempName), $"{Properties.Resources.ExposureTemplate} V1", new TemplateAutoExpTime());
            context.AddTemplateJsonPanel(nameof(node.TempName), $"{Properties.Resources.ExposureTemplate} V2", new TemplateAutoExpTimeV2());
        }
    }
}
