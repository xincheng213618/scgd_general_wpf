using System.Windows.Controls;
using System.ComponentModel;
using ColorVision.Common.Utilities;
using System.Windows.Input;

namespace System.Windows
{
    public sealed class WindowStatus
    {
        public object Root { get; set; }
        public Panel Parent { get; set; }
        public ContentControl ContentParent { get; set; }
        public WindowStyle WindowStyle { get; set; }

        public WindowState WindowState { get; set; }

        public ResizeMode ResizeMode { get; set; }
    }


    public interface IFullScreenState : INotifyPropertyChanged
    {
        bool IsFull { get; set; }
    }

    public static class WindowHelpers
    {
        private static readonly DependencyProperty FullScreenBindingProperty = DependencyProperty.RegisterAttached(
            "FullScreenBinding", typeof(FullScreenBinding), typeof(WindowHelpers));

        public static Window? GetActiveWindow(this Application application)
        {
            foreach (Window window in application.Windows)
                if (window.IsActive) return window;
            return Application.Current.MainWindow ;
        }

        /// <summary>
        /// 这里修改一下
        /// </summary>
        /// <returns></returns>
        public static Window? GetActiveWindow()
        {
            foreach (Window window in Application.Current.Windows)
                if (window.IsActive) return window;
            return Application.Current.MainWindow;
        }


        public static void SetWindowFull(this Window window, IFullScreenState  fullScreenState)
        {
            if (window.GetValue(FullScreenBindingProperty) is FullScreenBinding)
                return;
            window.SetValue(FullScreenBindingProperty, new FullScreenBinding(window, fullScreenState));
        }

        private sealed class FullScreenBinding
        {
            private readonly Window window;
            private readonly IFullScreenState config;
            private WindowFullScreenSession? session;

            public FullScreenBinding(Window window, IFullScreenState config)
            {
                this.window = window;
                this.config = config;
                config.PropertyChanged += OnConfigChanged;
                window.KeyDown += OnKeyDown;
                window.Loaded += OnLoaded;
                window.Closed += OnClosed;
                if (window.IsLoaded)
                    Apply();
            }

            private void OnKeyDown(object sender, KeyEventArgs e)
            {
                // Let focused content (such as ImageView) handle F11 first.
                // The active session owns exit keys during PreviewKeyDown.
                if (e.Handled || e.Key != Key.F11 || Keyboard.Modifiers != ModifierKeys.None || WindowFullScreenSession.GetIsActive(window))
                    return;
                e.Handled = true;
                if (!e.IsRepeat)
                    config.IsFull = true;
            }

            private void OnLoaded(object sender, RoutedEventArgs e) => Apply();

            private void OnConfigChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(IFullScreenState.IsFull))
                    Apply();
            }

            private void Apply()
            {
                if (!window.IsLoaded)
                    return;
                if (config.IsFull)
                {
                    session ??= new WindowFullScreenSession(window, () => config.IsFull = false);
                }
                else
                {
                    session?.Dispose();
                    session = null;
                }
            }
            private void OnClosed(object? sender, EventArgs e)
            {
                config.PropertyChanged -= OnConfigChanged;
                window.KeyDown -= OnKeyDown;
                window.Loaded -= OnLoaded;
                window.Closed -= OnClosed;
                session = null;
                window.ClearValue(FullScreenBindingProperty);
            }
        }
    }
}
