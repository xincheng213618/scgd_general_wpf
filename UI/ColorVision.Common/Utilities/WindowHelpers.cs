using System.Windows.Controls;
using System.ComponentModel;

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

        public static Window? GetActiveWindow(this Application application)
        {
            foreach (Window window in application.Windows)
                if (window.IsActive) return window;
            return null ;
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
            WindowStatus OldWindowStatus = null;

            //void PreviewKeyDown(object s, KeyEventArgs e)
            //{
            //    if (e.Key == Key.F11)
            //    {
            //        ToggleFull();
            //        e.Handled = true;
            //    }
            //}
            //window.PreviewKeyDown += PreviewKeyDown;

            void ToggleFull()
            {
                if (fullScreenState.IsFull)
                {
                    OldWindowStatus = new WindowStatus();
                    OldWindowStatus.WindowState = window.WindowState;
                    OldWindowStatus.WindowStyle = window.WindowStyle;
                    OldWindowStatus.ResizeMode = window.ResizeMode;
                    OldWindowStatus.Root = window.Content;
                    window.WindowStyle = WindowStyle.None;
                    window.WindowState = WindowState.Maximized;
                }
                else
                {
                    if (OldWindowStatus != null)
                    {
                        window.WindowStyle = OldWindowStatus.WindowStyle;
                        window.WindowState = OldWindowStatus.WindowState;
                        window.ResizeMode = OldWindowStatus.ResizeMode;
                        window.Content = OldWindowStatus.Root;
                    }
                }
            }
            fullScreenState.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(IFullScreenState.IsFull))
                {
                    ToggleFull();
                }
            };
        }
    }
}
