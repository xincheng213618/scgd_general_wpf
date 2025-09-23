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
        /// ��ͼ���ļ�
        /// </summary>
        public void OpenImage()
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _imageView.OpenImage(openFileDialog.FileName);
            }
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
        /// ��ʾ"���Ϊ"�Ի���
        /// </summary>
        public void SaveAs()
        {
            using var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "Png (*.png) | *.png";
            dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            Save(dialog.FileName);
        }

        /// <summary>
        /// ����ͼ��ָ���ļ�
        /// </summary>
        /// <param name="fileName">�ļ�·��</param>
        public void Save(string fileName)
        {
            RenderTargetBitmap renderTargetBitmap = new((int)_image.ActualWidth, (int)_image.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(_image);

            // ����һ��PngBitmapEncoder����������λͼΪPNG�ļ�
            PngBitmapEncoder pngEncoder = new();
            pngEncoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

            // ��PNG���ݱ��浽�ļ�
            using FileStream fileStream = new(fileName, FileMode.Create);
            pngEncoder.Save(fileStream);
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
