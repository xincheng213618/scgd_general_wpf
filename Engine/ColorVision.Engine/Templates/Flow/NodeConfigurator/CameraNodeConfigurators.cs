using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates.Jsons.HDR;
using ColorVision.Engine.Templates.Jsons.LedCheck2;
using ColorVision.Engine.Templates.Jsons.OLEDAOI;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.POIFilters;
using ColorVision.Engine.Templates.POI.POIRevise;
using System.Linq;

namespace ColorVision.Engine.Templates.Flow.NodeConfigurator
{
    [NodeConfigurator(typeof(FlowEngineLib.Node.Camera.CVAOICameraNode))]
    public class CVAOICameraNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Camera.CVAOICameraNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList());

            context.AddTemplatePanel(name => node.CamTempName = name, node.CamTempName, "相机模板", new TemplateCameraRunParam());
            context.AddTemplatePanel(name => node.TempName = name, node.TempName, "曝光模板", new TemplateAutoExpTime());
            var result = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList().Find(a => a.Code == node.DeviceCode);
            if (result?.PhyCamera != null)
                context.AddTemplatePanel(name => node.CalibTempName = name, node.CalibTempName, "校正", new TemplateCalibrationParam(result.PhyCamera));
            context.AddTemplateJsonPanel(name => node.AlgTempName = name, node.AlgTempName, "亚像素灯珠检测", new TemplateLedCheck2());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.Camera.CVAOI2CameraNode))]
    public class CVAOI2CameraNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Camera.CVAOI2CameraNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList());

            context.AddTemplatePanel(name => node.CamTempName = name, node.CamTempName, "相机模板", new TemplateCameraRunParam());
            context.AddTemplatePanel(name => node.TempName = name, node.TempName, "曝光模板", new TemplateAutoExpTime());
            var result = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList().Find(a => a.Code == node.DeviceCode);
            if (result?.PhyCamera != null)
                context.AddTemplatePanel(name => node.CalibTempName = name, node.CalibTempName, "校正", new TemplateCalibrationParam(result.PhyCamera));
            context.AddTemplateJsonPanel(name => node.AlgTempName = name, node.AlgTempName, "AOI", new TemplateOLEDAOI());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.CamMotorNode))]
    public class CamMotorNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.CamMotorNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList());
            context.AddTemplatePanel(name => node.AutoFocusTemp = name, node.AutoFocusTemp, "相机模板", new TemplateAutoFocus());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.Camera.CommCameraNode))]
    public class CommCameraNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Camera.CommCameraNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList());
            var result = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList().Find(a => a.Code == node.DeviceCode);
            if (result?.PhyCamera != null)
                context.AddTemplatePanel(name => node.CalibTempName = name, node.CalibTempName, "校正", new TemplateCalibrationParam(result.PhyCamera));
            context.AddTemplatePanel(name => node.CamTempName = name, node.CamTempName, "相机模板", new TemplateCameraRunParam());
            context.AddTemplateJsonPanel(name => node.CamTempName = name, node.CamTempName, "HDR模板", new TemplateHDR());
            context.AddTemplatePanel(name => node.TempName = name, node.TempName, "曝光模板", new TemplateAutoExpTime());

            context.AddTemplatePanel(name => node.POITempName = name, node.POITempName, "POI模板", new TemplatePoi());
            context.AddTemplatePanel(name => node.POIFilterTempName = name, node.POIFilterTempName, "POI过滤", new TemplatePoiFilterParam());
            context.AddTemplatePanel(name => node.POIReviseTempName = name, node.POIReviseTempName, "POI修正", new TemplatePoiReviseParam());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.CVCameraNode))]
    public class CVCameraNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.CVCameraNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList());

            var result = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList().Find(a => a.Code == node.DeviceCode);
            if (result?.PhyCamera != null)
                context.AddTemplatePanel(name => node.CalibTempName = name, node.CalibTempName, "校正", new TemplateCalibrationParam(result.PhyCamera));

            context.AddTemplatePanel(name => node.POITempName = name, node.POITempName, "POI模板", new TemplatePoi());
            context.AddTemplatePanel(name => node.POIFilterTempName = name, node.POIFilterTempName, "POI过滤", new TemplatePoiFilterParam());
            context.AddTemplatePanel(name => node.POIReviseTempName = name, node.POIReviseTempName, "POI修正", new TemplatePoiReviseParam());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.LVCameraNode))]
    public class LVCameraNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.LVCameraNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList());
            var result = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList().Find(a => a.Code == node.DeviceCode);
            if (result?.PhyCamera != null)
                context.AddTemplatePanel(name => node.CaliTempName = name, node.CaliTempName, "校正", new TemplateCalibrationParam(result.PhyCamera));

            context.AddTemplatePanel(name => node.POITempName = name, node.POITempName, "POI模板", new TemplatePoi());
            context.AddTemplatePanel(name => node.POIFilterTempName = name, node.POIFilterTempName, "POI过滤", new TemplatePoiFilterParam());
            context.AddTemplatePanel(name => node.POIReviseTempName = name, node.POIReviseTempName, "POI修正", new TemplatePoiReviseParam());
        }
    }
}
