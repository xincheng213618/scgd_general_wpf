using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// 图像键盘事件处理器
    /// </summary>
    public class ImageKeyboardHandler
    {
        private readonly ImageView _imageView;
        private readonly ImageViewModel _viewModel;
        private readonly Zoombox _zoomboxSub;
        private readonly ImageViewConfig _config;

        public ImageKeyboardHandler(
            ImageView ImageView,
            ImageViewModel viewModel,
            Zoombox zoomboxSub,
            ImageViewConfig config)
        {
            _imageView = ImageView ?? throw new ArgumentNullException(nameof(ImageView));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _zoomboxSub = zoomboxSub ?? throw new ArgumentNullException(nameof(zoomboxSub));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }
    }
}
