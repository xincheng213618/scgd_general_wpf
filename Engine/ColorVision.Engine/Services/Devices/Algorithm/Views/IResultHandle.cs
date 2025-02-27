using MQTTMessageLib.Algorithm;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{
    public interface IResultHandle
    {
        public List<AlgorithmResultType> CanHandle { get; }

        void Handle(AlgorithmView view, AlgorithmResult result);
        void SideSave(AlgorithmResult result, string selectedPath);
    }

    public abstract class IResultHandleBase : IResultHandle
    {
        public abstract List<AlgorithmResultType> CanHandle { get; } 

        public abstract void Handle(AlgorithmView view, AlgorithmResult result);
        public virtual void SideSave(AlgorithmResult result, string selectedPath)
        {

        }
    }

}
