using ColorVision.Engine.Services.Devices.Spectrum;
using FlowEngineLib.Node.Spectrum;
using FlowEngineLib.Spectum;

namespace ColorVision.Engine.Templates.Flow.NodeConfigurator
{
    [NodeConfigurator(typeof(SpectrumEQENode))]
    public class SpectrumEQENodeConfigurator : DeviceOnlyNodeConfigurator<SpectrumEQENode, DeviceSpectrum>
    {
    }

    [NodeConfigurator(typeof(SpectrumNode))]
    public class SpectrumNodeConfigurator : DeviceOnlyNodeConfigurator<SpectrumNode, DeviceSpectrum>
    {
    }

    [NodeConfigurator(typeof(SpectrumLoopNode))]
    public class SpectrumLoopNodeConfigurator : DeviceOnlyNodeConfigurator<SpectrumLoopNode, DeviceSpectrum>
    {
    }
}
