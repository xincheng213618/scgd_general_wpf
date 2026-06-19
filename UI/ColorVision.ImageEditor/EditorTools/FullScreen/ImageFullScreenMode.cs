using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.FullScreen
{
    public class ImageFullScreenMode
    {
        private readonly FrameworkElement _parent;
        private PlacementStatus? _oldWindowStatus;

        public bool IsMax { get; private set; }

        public ImageFullScreenMode(FrameworkElement parent)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        }

        public void ToggleFullScreen()
        {
            var window = Window.GetWindow(_parent);
            if (window == null) return;
            if (!IsMax) EnterFullScreen(window);
            else ExitFullScreen(window);
        }

        private void EnterFullScreen(Window window)
        {
            if (_parent.Parent is Panel panel)
            {
                _oldWindowStatus = new PlacementStatus { Parent = panel, WindowState = window.WindowState, WindowStyle = window.WindowStyle, ResizeMode = window.ResizeMode, Root = window.Content };
                panel.Children.Remove(_parent);
            }
            else if (_parent.Parent is ContentControl content)
            {
                _oldWindowStatus = new PlacementStatus { ContentParent = content, WindowState = window.WindowState, WindowStyle = window.WindowStyle, ResizeMode = window.ResizeMode, Root = window.Content };
                content.Content = null;
            }
            else return;

            IsMax = true;
            window.WindowState = WindowState.Normal;
            window.WindowStyle = WindowStyle.None;
            window.ResizeMode = ResizeMode.NoResize;
            window.WindowState = WindowState.Maximized;
            window.Content = _parent;
            window.PreviewKeyDown -= Window_PreviewKeyDown;
            window.PreviewKeyDown += Window_PreviewKeyDown;
        }

        private void ExitFullScreen(Window window)
        {
            if (_oldWindowStatus == null) return;

            IsMax = false;
            window.Content = _oldWindowStatus.Root;

            if (_oldWindowStatus.Parent != null) _oldWindowStatus.Parent.Children.Add(_parent);
            else if (_oldWindowStatus.ContentParent != null) _oldWindowStatus.ContentParent.Content = _parent;

            window.WindowStyle = _oldWindowStatus.WindowStyle;
            window.ResizeMode = _oldWindowStatus.ResizeMode;
            window.WindowState = _oldWindowStatus.WindowState;

            _oldWindowStatus = null;
            window.PreviewKeyDown -= Window_PreviewKeyDown;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!IsMax || (e.Key != Key.Escape && e.Key != Key.F11)) return;
            ToggleFullScreen();
            e.Handled = true;
        }
    }
}
