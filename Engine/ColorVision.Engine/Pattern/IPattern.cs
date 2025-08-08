using ColorVision.Common.MVVM;
using System.Windows.Controls;

namespace ColorVision.Engine.Pattern
{
    public interface IPattern
    {
        ViewModelBase GetConfig();

        UserControl GetPatternEditor();
        OpenCvSharp.Mat Gen(int height, int  width);
    }
}
