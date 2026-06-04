using ColorVision.Engine.Properties;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.Devices.Camera.Templates.HDR;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates.Jsons.LedCheck2;
using ColorVision.Engine.Templates.Jsons.OLEDAOI;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.POIFilters;
using ColorVision.Engine.Templates.POI.POIRevise;
using FlowEngineLib;
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

            context.AddTemplatePanel(name => node.CamTempName = name, node.CamTempName, Properties.Resources.CameraTemplate, new TemplateCameraRunParam());
            context.AddTemplatePanel(name => node.TempName = name, node.TempName, Properties.Resources.ExposureTemplate, new TemplateAutoExpTime());
            var result = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList().Find(a => a.Code == node.DeviceCode);
            if (result?.PhyCamera != null)
                context.AddTemplatePanel(name => node.CalibTempName = name, node.CalibTempName, Properties.Resources.Calibration, new TemplateCalibrationParam(result.PhyCamera));
            context.AddTemplateJsonPanel(name => node.AlgTempName = name, node.AlgTempName, Properties.Resources.SubPixelLedCheck, new TemplateLedCheck2());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.AOILocatePixelsCameraNode))]
    public class AOILocatePixelsCameraNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (AOILocatePixelsCameraNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList());

            context.AddTemplatePanel(name => node.AutoExpTempName = name, node.AutoExpTempName, Properties.Resources.ExposureTemplate, new TemplateAutoExpTime());
            var result = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList().Find(a => a.Code == node.DeviceCode);
            if (result?.PhyCamera != null)
                context.AddTemplatePanel(name => node.CaliTempName = name, node.CaliTempName, Properties.Resources.Calibration, new TemplateCalibrationParam(result.PhyCamera));
            context.AddTemplateJsonPanel(name => node.AlgTempName = name, node.AlgTempName, Properties.Resources.SubPixelLedCheck, new TemplateLedCheck2());
        }
    }
    [NodeConfigurator(typeof(FlowEngineLib.AOILocAndRegPixelsCameraNode))]
    public class AOILocAndRegPixelsCameraNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (AOILocAndRegPixelsCameraNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList());

            context.AddTemplatePanel(name => node.AutoExpTempName = name, node.AutoExpTempName, Properties.Resources.ExposureTemplate, new TemplateAutoExpTime());
            var result = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList().Find(a => a.Code == node.DeviceCode);
            if (result?.PhyCamera != null)
                context.AddTemplatePanel(name => node.CaliTempName = name, node.CaliTempName, Properties.Resources.Calibration, new TemplateCalibrationParam(result.PhyCamera));
            context.AddTemplateJsonPanel(name => node.AlgTempName = name, node.AlgTempName, "AOI", new TemplateOLEDAOI());
        }
    }
    

    [NodeConfigurator(typeof(FlowEngineLib.Node.Camera.CVAOI2CameraNode))]
    public class CVAOI2CameraNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Camera.CVAOI2CameraNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList());

            context.AddTemplatePanel(name => node.CamTempName = name, node.CamTempName, Properties.Resources.CameraTemplate, new TemplateCameraRunParam());
            context.AddTemplatePanel(name => node.TempName = name, node.TempName, Properties.Resources.ExposureTemplate, new TemplateAutoExpTime());
            var result = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList().Find(a => a.Code == node.DeviceCode);
            if (result?.PhyCamera != null)
                context.AddTemplatePanel(name => node.CalibTempName = name, node.CalibTempName, Properties.Resources.Calibration, new TemplateCalibrationParam(result.PhyCamera));
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
            context.AddTemplatePanel(name => node.AutoFocusTemp = name, node.AutoFocusTemp, Properties.Resources.CameraTemplate, new TemplateAutoFocus());
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
                context.AddTemplatePanel(name => node.CalibTempName = name, node.CalibTempName, Properties.Resources.Calibration, new TemplateCalibrationParam(result.PhyCamera));
            context.AddTemplatePanel(name => node.CamTempName = name, node.CamTempName, Properties.Resources.CameraTemplate, new TemplateCameraRunParam());
            context.AddTemplateJsonPanel(name => node.CamTempName = name, node.CamTempName, Properties.Resources.HdrTemplate, new TemplateHDR());
            context.AddTemplatePanel(name => node.TempName = name, node.TempName, Properties.Resources.ExposureTemplate, new TemplateAutoExpTime());

            context.AddTemplatePanel(name => node.POITempName = name, node.POITempName, Properties.Resources.POITemplate, new TemplatePoi());
            context.AddTemplatePanel(name => node.POIFilterTempName = name, node.POIFilterTempName, Properties.Resources.POIFilter, new TemplatePoiFilterParam());
            context.AddTemplatePanel(name => node.POIReviseTempName = name, node.POIReviseTempName, Properties.Resources.POIRevise, new TemplatePoiReviseParam());
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
                context.AddTemplatePanel(name => node.CalibTempName = name, node.CalibTempName, Properties.Resources.Calibration, new TemplateCalibrationParam(result.PhyCamera));

            context.AddTemplatePanel(name => node.POITempName = name, node.POITempName, Properties.Resources.POITemplate, new TemplatePoi());
            context.AddTemplatePanel(name => node.POIFilterTempName = name, node.POIFilterTempName, Properties.Resources.POIFilter, new TemplatePoiFilterParam());
            context.AddTemplatePanel(name => node.POIReviseTempName = name, node.POIReviseTempName, Properties.Resources.POIRevise, new TemplatePoiReviseParam());
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
                context.AddTemplatePanel(name => node.CaliTempName = name, node.CaliTempName, Properties.Resources.Calibration, new TemplateCalibrationParam(result.PhyCamera));

            context.AddTemplatePanel(name => node.POITempName = name, node.POITempName, Properties.Resources.POITemplate, new TemplatePoi());
            context.AddTemplatePanel(name => node.POIFilterTempName = name, node.POIFilterTempName, Properties.Resources.POIFilter, new TemplatePoiFilterParam());
            context.AddTemplatePanel(name => node.POIReviseTempName = name, node.POIReviseTempName, Properties.Resources.POIRevise, new TemplatePoiReviseParam());
        }
    }
}
