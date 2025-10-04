using ColorVision.ImageEditor.Draw.Ruler;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// 图像文件操作类，处理图像的打开、保存、打印等操作
    /// </summary>
    public class ImageFileOperations
    {
        private readonly ImageView _imageView;
        private readonly DrawCanvas _image;

        public ImageFileOperations(DrawCanvas image, ImageView imageView)
        {
            _imageView = imageView ?? throw new ArgumentNullException(nameof(imageView));

            _image = image ?? throw new ArgumentNullException(nameof(image));
        }

        /// <summary>
        /// 清除图像
        /// </summary>
        /// <param name="toolBarScaleRuler">标尺工具栏</param>
        /// <param name="clearImageEventHandler">清除图像事件处理器</param>
        public void ClearImage(ToolBarScaleRuler toolBarScaleRuler, EventHandler clearImageEventHandler)
        {
            _image.Clear();
            _image.Source = null;
            _image.UpdateLayout();

            toolBarScaleRuler.IsShow = false;
            clearImageEventHandler?.Invoke(this, new EventArgs());
        }

        /// <summary>
        /// 打印图像
        /// </summary>
        public void Print()
        {
            PrintDialog printDialog = new();
            if (printDialog.ShowDialog() == true)
            {
                // 创建一个可打印的区域
                Size pageSize = new(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);
                _image.Measure(pageSize);
                _image.Arrange(new Rect(5, 5, pageSize.Width, pageSize.Height));

                // 开始打印
                printDialog.PrintVisual(_image, "Printing");
            }
        }
    }
}
