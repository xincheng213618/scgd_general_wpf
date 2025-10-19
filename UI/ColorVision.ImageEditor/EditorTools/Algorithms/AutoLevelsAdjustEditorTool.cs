using ColorVision.Common.MVVM;
using ColorVision.Core;
using log4net;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    public class AutoLevelsAdjustEditorTool : IEditorToggleToolBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AutoLevelsAdjustEditorTool));
        private readonly EditorContext editorContext;

        public AutoLevelsAdjustEditorTool(EditorContext context)
        {
            editorContext = context;
            ToolBarLocal = ToolBarLocal.Right;
            Order = 30;
            Icon = IEditorToolFactory.TryFindResource("DrawingImageMax");
            Command = new RelayCommand(_ => Execute());
        }

        private void Execute()
        {
            var imageView = editorContext.ImageView;
            if (imageView == null) return;

            if (!IsChecked)
            {
                imageView.ImageShow.Source = imageView.ViewBitmapSource;
                imageView.FunctionImage = null;
                return;
            }

            if (imageView.HImageCache != null)
            {
                int ret = OpenCVMediaHelper.M_AutoLevelsAdjust((HImage)imageView.HImageCache, out HImage hImageProcessed);
                if (ret == 0)
                {
                    if (!HImageExtension.UpdateWriteableBitmap(imageView.FunctionImage, hImageProcessed))
                    {
                        double DpiX = imageView.Config.GetProperties<double>("DpiX");
                        double DpiY = imageView.Config.GetProperties<double>("DpiY");
                        var image = hImageProcessed.ToWriteableBitmap(DpiX, DpiY);

                        hImageProcessed.Dispose();
                        imageView.FunctionImage = image;
                    }
                    imageView.ImageShow.Source = imageView.FunctionImage;
                }
            }
        }
    }
}
