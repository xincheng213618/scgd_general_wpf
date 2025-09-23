using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// 图像全屏模式管理类
    /// </summary>
    public class ImageFullScreenMode
    {
        private readonly FrameworkElement _parent;
        private ImagePlacementContext _oldWindowStatus;
        
        public bool IsMax { get; private set; }

        public ImageFullScreenMode(FrameworkElement parent)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        }

        /// <summary>
        /// 切换全屏模式
        /// </summary>
        public void ToggleFullScreen()
        {
            void PreviewKeyDown(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Escape || e.Key == Key.F11)
                {
                    if (IsMax)
                        ToggleFullScreen();
                }
            }

            var window = Window.GetWindow(_parent);
            if (!IsMax)
            {
                IsMax = true;
                if (_parent.Parent is Panel p)
                {
                    _oldWindowStatus = new ImagePlacementContext();
                    _oldWindowStatus.Parent = p;
                    _oldWindowStatus.WindowState = window.WindowState;
                    _oldWindowStatus.WindowStyle = window.WindowStyle;
                    _oldWindowStatus.ResizeMode = window.ResizeMode;
                    _oldWindowStatus.Root = window.Content;
                    window.WindowStyle = WindowStyle.None;
                    window.WindowState = WindowState.Maximized;

                    _oldWindowStatus.Parent.Children.Remove(_parent);
                    window.Content = _parent;

                    window.PreviewKeyDown -= PreviewKeyDown;
                    window.PreviewKeyDown += PreviewKeyDown;
                }
                else if (_parent.Parent is ContentControl content)
                {
                    _oldWindowStatus = new ImagePlacementContext();
                    _oldWindowStatus.ContentParent = content;
                    _oldWindowStatus.WindowState = window.WindowState;
                    _oldWindowStatus.WindowStyle = window.WindowStyle;
                    _oldWindowStatus.ResizeMode = window.ResizeMode;
                    _oldWindowStatus.Root = window.Content;
                    window.WindowStyle = WindowStyle.None;
                    window.WindowState = WindowState.Maximized;

                    content.Content = null;
                    window.Content = _parent;
                    window.PreviewKeyDown -= PreviewKeyDown;
                    window.PreviewKeyDown += PreviewKeyDown;
                }
            }
            else
            {
                IsMax = false;
                if (_oldWindowStatus.Parent != null)
                {
                    window.WindowStyle = _oldWindowStatus.WindowStyle;
                    window.WindowState = _oldWindowStatus.WindowState;
                    window.ResizeMode = _oldWindowStatus.ResizeMode;

                    window.Content = _oldWindowStatus.Root;
                    _oldWindowStatus.Parent.Children.Add(_parent);
                }
                else
                {
                    window.WindowStyle = _oldWindowStatus.WindowStyle;
                    window.WindowState = _oldWindowStatus.WindowState;
                    window.ResizeMode = _oldWindowStatus.ResizeMode;

                    _oldWindowStatus.ContentParent.Content = _parent;
                }
                window.PreviewKeyDown -= PreviewKeyDown;
            }
        }
    }
}
