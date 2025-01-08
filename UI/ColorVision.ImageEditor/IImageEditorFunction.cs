#pragma warning disable CS8625
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{
    public interface IImageEditorFunction
    {
        object Header { get; }

        string Description { get; }

        Control GetUserControl();

        int Execute(HImage hImage, out HImage hImageOut);
    }

    public abstract class ImageEditorFunctionBase : IImageEditorFunction
    {
        public abstract object Header { get; }
        public virtual string Description { get; }

        public abstract int Execute(HImage hImage, out HImage hImageOut);

        public virtual Control GetUserControl()
        {
            return null;
        }
    }

    public class FuncAutoLevelsAdjust : ImageEditorFunctionBase
    {
        public override object Header => "自动对比度";
        public override string Description { get; } = "自动对比度";

        public override Control GetUserControl()
        {
            Button button = new Button() { Content = "自动对比度" };
            return button;
        }

        public override int Execute(HImage hImage, out HImage hImageOut)
        {
            int ret = OpenCVMediaHelper.M_AutoLevelsAdjust(hImage, out hImageOut);
            return ret;
        }
    }

}
