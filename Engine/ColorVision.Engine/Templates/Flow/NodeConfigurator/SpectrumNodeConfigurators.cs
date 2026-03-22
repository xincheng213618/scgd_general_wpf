using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Spectrum;
using System.Linq;

namespace ColorVision.Engine.Templates.Flow.NodeConfigurator
{
    [NodeConfigurator(typeof(FlowEngineLib.Node.Spectrum.SpectrumEQENode))]
    public class SpectrumEQENodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Spectrum.SpectrumEQENode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceSpectrum>().ToList());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.Spectrum.SpectrumNode))]
    public class SpectrumNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Spectrum.SpectrumNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceSpectrum>().ToList());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Spectum.SpectrumLoopNode))]
    public class SpectrumLoopNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Spectum.SpectrumLoopNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceSpectrum>().ToList());
        }
    }
}
