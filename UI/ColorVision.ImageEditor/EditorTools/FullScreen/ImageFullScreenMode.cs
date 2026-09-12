using ColorVision.Common.Utilities;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.EditorTools.FullScreen
{
    public class ImageFullScreenMode
    {
        private readonly FrameworkElement _parent;
        private PlacementStatus? _oldWindowStatus;
        private WindowFullScreenSession? _windowSession;
        private int _childIndex;

        public bool IsMax { get; private set; }
        public event EventHandler? FullScreenChanged;

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
                _childIndex = panel.Children.IndexOf(_parent);
                panel.Children.Remove(_parent);
            }
            else if (_parent.Parent is ContentControl content)
            {
                _oldWindowStatus = new PlacementStatus { ContentParent = content, WindowState = window.WindowState, WindowStyle = window.WindowStyle, ResizeMode = window.ResizeMode, Root = window.Content };
                content.Content = null;
            }
            else return;

            IsMax = true;
            try
            {
                _windowSession = new WindowFullScreenSession(window, () => ExitFullScreen(window));
                window.Content = _parent;
                window.UpdateLayout();
                FullScreenChanged?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                ExitFullScreen(window);
                throw;
            }
        }

        private void ExitFullScreen(Window window)
        {
            if (_oldWindowStatus == null) return;

            IsMax = false;
            window.Content = _oldWindowStatus.Root;

            if (_oldWindowStatus.Parent != null) _oldWindowStatus.Parent.Children.Insert(_childIndex, _parent);
            else if (_oldWindowStatus.ContentParent != null) _oldWindowStatus.ContentParent.Content = _parent;

            _windowSession?.Dispose();
            _windowSession = null;

            _oldWindowStatus = null;
            window.UpdateLayout();
            FullScreenChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
