using System.Windows.Controls;

namespace ColorVision.Engine.Pattern
{
    public interface IPattern
    {
        UserControl GetPatternEditor();
        OpenCvSharp.Mat Gen(int height, int  width);
    }
}
