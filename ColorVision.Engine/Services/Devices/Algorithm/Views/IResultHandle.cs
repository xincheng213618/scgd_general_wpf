using MQTTMessageLib.Algorithm;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{
    public interface IResultHandle
    {
        AlgorithmResultType ResultType { get; }
        void Handle(AlgorithmView view, AlgorithmResult result);
    }
}
