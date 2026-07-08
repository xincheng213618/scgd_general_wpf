using ColorVision.Engine.Properties;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.Devices.Camera.Templates.HDR;
using ColorVision.Engine.Templates.Jsons.AutoExpTime;
using FlowEngineLib;

namespace ColorVision.Engine.Templates.Flow.NodeConfigurator
{
    [NodeConfigurator(typeof(FlowEngineLib.Node.Camera.CVAOICameraNode))]
    public class CVAOICameraNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Camera.CVAOICameraNode)context.Node;

            context.AddTemplatePanel(name => node.TempName = name, node.TempName, $"{Properties.Resources.ExposureTemplate} V1", new TemplateAutoExpTime());
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, $"{Properties.Resources.ExposureTemplate} V2", new TemplateAutoExpTimeV2());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.AOILocatePixelsCameraNode))]
    public class AOILocatePixelsCameraNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (AOILocatePixelsCameraNode)context.Node;

            context.AddTemplatePanel(name => node.AutoExpTempName = name, node.AutoExpTempName, $"{Properties.Resources.ExposureTemplate} V1", new TemplateAutoExpTime());
            context.AddTemplateJsonPanel(name => node.AutoExpTempName = name, node.AutoExpTempName, $"{Properties.Resources.ExposureTemplate} V2", new TemplateAutoExpTimeV2());
        }
    }
    [NodeConfigurator(typeof(FlowEngineLib.AOILocAndRegPixelsCameraNode))]
    public class AOILocAndRegPixelsCameraNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (AOILocAndRegPixelsCameraNode)context.Node;

            context.AddTemplatePanel(name => node.AutoExpTempName = name, node.AutoExpTempName, $"{Properties.Resources.ExposureTemplate} V1", new TemplateAutoExpTime());
            context.AddTemplateJsonPanel(name => node.AutoExpTempName = name, node.AutoExpTempName, $"{Properties.Resources.ExposureTemplate} V2", new TemplateAutoExpTimeV2());
        }
    }
    

    [NodeConfigurator(typeof(FlowEngineLib.Node.Camera.CVAOI2CameraNode))]
    public class CVAOI2CameraNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Camera.CVAOI2CameraNode)context.Node;

            context.AddTemplatePanel(name => node.TempName = name, node.TempName, $"{Properties.Resources.ExposureTemplate} V1", new TemplateAutoExpTime());
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, $"{Properties.Resources.ExposureTemplate} V2", new TemplateAutoExpTimeV2());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.Camera.CommCameraNode))]
    public class CommCameraNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Camera.CommCameraNode)context.Node;
            context.AddTemplatePanel(name => node.CamTempName = name, node.CamTempName, Properties.Resources.CameraTemplate, new TemplateCameraRunParam());
            context.AddTemplateJsonPanel(name => node.CamTempName = name, node.CamTempName, Properties.Resources.HdrTemplate, new TemplateHDR());
            context.AddTemplatePanel(name => node.TempName = name, node.TempName, $"{Properties.Resources.ExposureTemplate} V1", new TemplateAutoExpTime());
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, $"{Properties.Resources.ExposureTemplate} V2", new TemplateAutoExpTimeV2());
        }
    }
}
