using ColorVision.Common.MVVM;
using System.Windows.Controls;

namespace ColorVision.Engine.Pattern
{
    public interface IPattern
    {
        ViewModelBase GetConfig();

        UserControl GetPatternEditor();
        OpenCvSharp.Mat Gen(int height, int width);
    }

    public abstract class IPatternBase : ViewModelBase, IPattern
    {
        public abstract ViewModelBase GetConfig();
        public abstract UserControl GetPatternEditor();
        public abstract OpenCvSharp.Mat Gen(int height, int width);

    }
}