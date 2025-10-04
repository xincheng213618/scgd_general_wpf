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
    /// ͼ���ļ������࣬����ͼ��Ĵ򿪡����桢��ӡ�Ȳ���
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
        /// ���ͼ��
        /// </summary>
        /// <param name="toolBarScaleRuler">��߹�����</param>
        /// <param name="clearImageEventHandler">���ͼ���¼�������</param>
        public void ClearImage(ToolBarScaleRuler toolBarScaleRuler, EventHandler clearImageEventHandler)
        {
            _image.Clear();
            _image.Source = null;
            _image.UpdateLayout();

            toolBarScaleRuler.IsShow = false;
            clearImageEventHandler?.Invoke(this, new EventArgs());
        }

        /// <summary>
        /// ��ӡͼ��
        /// </summary>
        public void Print()
        {
            PrintDialog printDialog = new();
            if (printDialog.ShowDialog() == true)
            {
                // ����һ���ɴ�ӡ������
                Size pageSize = new(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);
                _image.Measure(pageSize);
                _image.Arrange(new Rect(5, 5, pageSize.Width, pageSize.Height));

                // ��ʼ��ӡ
                printDialog.PrintVisual(_image, "Printing");
            }
        }
    }
}
