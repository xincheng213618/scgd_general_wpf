using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.Windows.Controls;

namespace ColorVision.Engine.Pattern
{
    public interface IPattern
    {
        ViewModelBase GetConfig();
        void SetConfig(string config);

        UserControl GetPatternEditor();
        OpenCvSharp.Mat Gen(int height, int width);
    }

    public abstract class IPatternBase : ViewModelBase, IPattern
    {
        public abstract ViewModelBase GetConfig();
        public abstract void SetConfig(string config);

        public abstract UserControl GetPatternEditor();
        public abstract OpenCvSharp.Mat Gen(int height, int width);
    }

    public abstract class IPatternBase<T> : IPatternBase where T:ViewModelBase,new()
    {
        public T Config { get; set; } = new T();
        public override ViewModelBase GetConfig() => Config;
        public override void SetConfig(string config)
        {
            Config = JsonConvert.DeserializeObject<T>(config) ?? new T();
        }

    }

}