using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.Windows.Controls;

namespace Pattern
{
    /// <summary>
    /// 图案尺寸模式：按视场系数或按像素尺寸
    /// </summary>
    public enum PatternSizeMode
    {
        /// <summary>
        /// 按视场系数（0-1.0）
        /// </summary>
        ByFieldOfView,
        /// <summary>
        /// 按像素尺寸
        /// </summary>
        ByPixelSize
    }

    public interface IPattern
    {
        ViewModelBase GetConfig();
        void SetConfig(string config);

        UserControl GetPatternEditor();
        OpenCvSharp.Mat Gen(int height, int width);
        string GetTemplateName();
    }

    public abstract class IPatternBase : ViewModelBase, IPattern
    {
        public abstract ViewModelBase GetConfig();
        public abstract void SetConfig(string config);

        public abstract UserControl GetPatternEditor();
        public abstract OpenCvSharp.Mat Gen(int height, int width);

        public abstract string GetTemplateName();

    }

    public abstract class IPatternBase<T> : IPatternBase where T:ViewModelBase,new()
    {
        public T Config { get; set; } = new T();
        public override ViewModelBase GetConfig() => Config;
        public override void SetConfig(string config)
        {
            Config = JsonConvert.DeserializeObject<T>(config) ?? new T();
        }

        public override string GetTemplateName()
        {
            return GetType().ToString() + "_" +DateTime.Now.ToString("HHmmss");
        }

    }

}