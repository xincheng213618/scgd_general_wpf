using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// ͼ������¼�������
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

        /// <summary>
        /// ��������¼�
        /// </summary>
        /// <param name="sender">�¼�������</param>
        /// <param name="e">�����¼�����</param>
        public void HandleKeyDown(object sender, KeyEventArgs e)
        {
            // F11ȫ������
            //if (e.Key == Key.F11)
            //{
            //    if (!_viewModel.IsMax)
            //        _viewModel.FullCommand.Execute(null);
            //    e.Handled = true;
            //    return;
            //}

            // �༭ģʽ�µļ��̲���
            if (_viewModel.ImageEditMode)
            {
                HandleEditModeKeyDown(e);
            }
            // ���ģʽ�µļ��̲���
            else
            {
                HandleBrowseModeKeyDown(e);
            }
        }

        /// <summary>
        /// ����༭ģʽ�µļ��̲���
        /// </summary>
        private void HandleEditModeKeyDown(KeyEventArgs e)
        {
             if (Keyboard.IsKeyDown(Key.LeftCtrl) && (e.Key == Key.Left || e.Key == Key.A))
            {
                MoveView(-10, 0);
                e.Handled = true;
            }
            else if (Keyboard.IsKeyDown(Key.LeftCtrl) && (e.Key == Key.Right || e.Key == Key.D))
            {
                MoveView(10, 0);
                e.Handled = true;
            }
            else if (Keyboard.IsKeyDown(Key.LeftCtrl) && (e.Key == Key.Up || e.Key == Key.W))
            {
                MoveView(0, -10);
                e.Handled = true;
            }
            else if (Keyboard.IsKeyDown(Key.LeftCtrl) && (e.Key == Key.Down || e.Key == Key.S))
            {
                MoveView(0, 10);
                e.Handled = true;
            }
        }

        /// <summary>
        /// �������ģʽ�µļ��̲���
        /// </summary>
        private void HandleBrowseModeKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                MoveView(-10, 0);
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                MoveView(10, 0);
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                // �л�����һ���ļ�
                string? previousFile = GetAdjacentImageFile(_config.FilePath, false);
                if (!string.IsNullOrEmpty(previousFile))
                {
                    _imageView.OpenImage(previousFile);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                // �л�����һ���ļ�
                string? nextFile = GetAdjacentImageFile(_config.FilePath, true);
                if (!string.IsNullOrEmpty(nextFile))
                {
                    _imageView.OpenImage(nextFile);
                }
                e.Handled = true;
            }
        }

        /// <summary>
        /// �ƶ���ͼλ��
        /// </summary>
        /// <param name="x">X�����ƶ���</param>
        /// <param name="y">Y�����ƶ���</param>
        private void MoveView(double x, double y)
        {
            TranslateTransform translateTransform = new();
            Vector vector = new(x, y);
            translateTransform.SetCurrentValue(TranslateTransform.XProperty, vector.X);
            translateTransform.SetCurrentValue(TranslateTransform.YProperty, vector.Y);
            _zoomboxSub.SetCurrentValue(Zoombox.ContentMatrixProperty, 
                Matrix.Multiply(_zoomboxSub.ContentMatrix, translateTransform.Value));
        }

        /// <summary>
        /// ��ȡ���ڵ�ͼ���ļ�
        /// </summary>
        /// <param name="currentFilePath">��ǰ�ļ�·��</param>
        /// <param name="moveNext">�Ƿ��ȡ��һ���ļ�</param>
        /// <returns>�����ļ���·��</returns>
        private string? GetAdjacentImageFile(string currentFilePath, bool moveNext)
        {
            var supportedExtensions = ComponentManager.GetInstance().IImageOpens.Keys.ToList();
            try
            {
                // ��ȡ��ǰ�ļ����ڵ�Ŀ¼
                string? directory = Path.GetDirectoryName(currentFilePath);
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    return null;
                }

                // ��ȡĿ¼������֧�ֵ�ͼƬ�ļ���������������
                var imageFiles = Directory.GetFiles(directory)
                    .Where(f => supportedExtensions.Contains(Path.GetExtension(f)))
                    .OrderBy(f => f)
                    .ToList();

                if (imageFiles.Count <= 1)
                {
                    return null; // �ļ�����û������ͼƬ
                }

                // ���б����ҵ���ǰ�ļ�������
                int currentIndex = imageFiles.FindIndex(
                    f => string.Equals(f, currentFilePath, StringComparison.OrdinalIgnoreCase));
                
                if (currentIndex == -1)
                {
                    return null; // ��ǰ�ļ������б��У���������������ɾ����
                }

                // ������һ������һ���ļ�������
                int newIndex;
                if (moveNext) // ��ȡ��һ��
                {
                    newIndex = (currentIndex + 1) % imageFiles.Count;
                }
                else // ��ȡ��һ��
                {
                    newIndex = (currentIndex - 1 + imageFiles.Count) % imageFiles.Count;
                }

                // �����µ��ļ�·��
                return imageFiles[newIndex];
            }
            catch (Exception ex)
            {
                // ���������־��¼
                Console.WriteLine($"Error finding adjacent image file: {ex.Message}");
                return null;
            }
        }
    }
}
