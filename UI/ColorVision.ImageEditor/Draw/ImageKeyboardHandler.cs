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

        /// <summary>
        /// 处理键盘事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">键盘事件参数</param>
        public void HandleKeyDown(object sender, KeyEventArgs e)
        {
            // F11全屏处理
            //if (e.Key == Key.F11)
            //{
            //    if (!_viewModel.IsMax)
            //        _viewModel.FullCommand.Execute(null);
            //    e.Handled = true;
            //    return;
            //}

            // 编辑模式下的键盘操作
            if (_viewModel.ImageEditMode)
            {
                HandleEditModeKeyDown(e);
            }
            // 浏览模式下的键盘操作
            else
            {
                HandleBrowseModeKeyDown(e);
            }
        }

        /// <summary>
        /// 处理编辑模式下的键盘操作
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
        /// 处理浏览模式下的键盘操作
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
                // 切换到上一个文件
                string? previousFile = GetAdjacentImageFile(_config.FilePath, false);
                if (!string.IsNullOrEmpty(previousFile))
                {
                    _imageView.OpenImage(previousFile);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                // 切换到下一个文件
                string? nextFile = GetAdjacentImageFile(_config.FilePath, true);
                if (!string.IsNullOrEmpty(nextFile))
                {
                    _imageView.OpenImage(nextFile);
                }
                e.Handled = true;
            }
        }

        /// <summary>
        /// 移动视图位置
        /// </summary>
        /// <param name="x">X方向移动量</param>
        /// <param name="y">Y方向移动量</param>
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
        /// 获取相邻的图像文件
        /// </summary>
        /// <param name="currentFilePath">当前文件路径</param>
        /// <param name="moveNext">是否获取下一个文件</param>
        /// <returns>相邻文件的路径</returns>
        private string? GetAdjacentImageFile(string currentFilePath, bool moveNext)
        {
            var supportedExtensions = ComponentManager.GetInstance().IImageOpens.Keys.ToList();
            try
            {
                // 获取当前文件所在的目录
                string? directory = Path.GetDirectoryName(currentFilePath);
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    return null;
                }

                // 获取目录中所有支持的图片文件，并按名称排序
                var imageFiles = Directory.GetFiles(directory)
                    .Where(f => supportedExtensions.Contains(Path.GetExtension(f)))
                    .OrderBy(f => f)
                    .ToList();

                if (imageFiles.Count <= 1)
                {
                    return null; // 文件夹中没有其他图片
                }

                // 在列表中找到当前文件的索引
                int currentIndex = imageFiles.FindIndex(
                    f => string.Equals(f, currentFilePath, StringComparison.OrdinalIgnoreCase));
                
                if (currentIndex == -1)
                {
                    return null; // 当前文件不在列表中（可能已重命名或删除）
                }

                // 计算上一个或下一个文件的索引
                int newIndex;
                if (moveNext) // 获取下一个
                {
                    newIndex = (currentIndex + 1) % imageFiles.Count;
                }
                else // 获取上一个
                {
                    newIndex = (currentIndex - 1 + imageFiles.Count) % imageFiles.Count;
                }

                // 返回新的文件路径
                return imageFiles[newIndex];
            }
            catch (Exception ex)
            {
                // 可以添加日志记录
                Console.WriteLine($"Error finding adjacent image file: {ex.Message}");
                return null;
            }
        }
    }
}
