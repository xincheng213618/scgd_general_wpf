using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.PG;
using ColorVision.Engine.Services.Devices.Sensor;
using ColorVision.Engine.Services.Devices.Sensor.Templates;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.PhyCameras.Group;
using System.Linq;

namespace ColorVision.Engine.Templates.Flow.NodeConfigurator
{
    [NodeConfigurator(typeof(FlowEngineLib.Node.PG.PGNode))]
    public class PGNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.PG.PGNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DevicePG>().ToList());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.FWNode))]
    public class FWNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.FWNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceCfwPort>().ToList());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.SMUModelNode))]
    public class SMUModelNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.SMUModelNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceSMU>().ToList());
            context.AddTemplatePanel(name => node.ModelName = name, node.ModelName, "SMUParam设置", new TemplateSMUParam());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.SMUFromCSVNode))]
    public class SMUFromCSVNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.SMUFromCSVNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceSMU>().ToList());
            context.AddImagePath(name => node.CsvFileName = name, node.CsvFileName, "CSV");
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.SMUNode))]
    public class SMUNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.SMUNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceSMU>().ToList());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.CommonSensorNode))]
    public class CommonSensorNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.CommonSensorNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceSensor>().ToList());
            context.AddTemplateCollectionPanel(name => node.TempName = name, node.TempName, "模板名称", TemplateSensor.AllParams);
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Algorithm.CalibrationNode))]
    public class CalibrationNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Algorithm.CalibrationNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceCalibration>().ToList());
            context.AddImagePath(name => node.ImgFileName = name, node.ImgFileName);

            var result = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCalibration>().ToList().Find(a => a.Code == node.DeviceCode);
            if (result?.PhyCamera != null)
                context.AddTemplatePanel(name => node.TempName = name, node.TempName, "校正", new TemplateCalibrationParam(result.PhyCamera));
        }
    }
}
