using ColorVision.Common.MVVM;
using ColorVision.Engine.Pattern.Stripe;
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
        public override void SetConfig(string config)
        {
            var b = JsonConvert.DeserializeObject<T>(config);
            var Config = GetConfig();
            Config.CopyFrom(b);
        }

    }

}