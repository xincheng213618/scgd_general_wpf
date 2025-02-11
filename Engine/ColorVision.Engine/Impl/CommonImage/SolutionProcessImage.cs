using ColorVision.Common.Utilities;
using ColorVision.ImageEditor;
using ColorVision.Solution;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Impl.CommonImage
{
    public class SolutionProcessImage : ISolutionProcess
    {
        public ImageView ImageView { get; set; } = ImageView.GetInstance();

        public string Name { get; set; }

        public ImageSource IconSource { get; set; }
        public Control UserControl => ImageView;

        public string FullName { get; set; }

        public string GuidId => Tool.GetMD5(FullName);

        public void Close()
        {
            ImageView.ImageViewModel.ClearImage();
        }

        public virtual void Open()
        {
            ImageView.OpenImage(FullName);
        }
    }

}
