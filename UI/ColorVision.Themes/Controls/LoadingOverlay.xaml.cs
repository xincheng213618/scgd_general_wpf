using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Themes.Controls
{
    /// <summary>
    /// LoadingOverlay.xaml 的交互逻辑
    /// </summary>
    public partial class LoadingOverlay : UserControl
    {
        private static readonly Dictionary<Window, LoadingOverlay> _instances = new Dictionary<Window, LoadingOverlay>();
        private static readonly object _lock = new object();


        public static LoadingOverlay GetInstance(Window? parentWindow, Action? onCancel = null)
        {
            lock (_lock)
            {
                ArgumentNullException.ThrowIfNull(parentWindow);
                if (!_instances.TryGetValue(parentWindow, out LoadingOverlay overlay))
                {
                    overlay = new LoadingOverlay() { Visibility = Visibility.Collapsed };
                    _instances[parentWindow] = overlay;
                    overlay.parentContent = parentWindow.Content;

                    if (overlay.parentContent is Grid parentGrid)
                    {
                        Grid.SetColumnSpan(overlay,3);
                        Grid.SetRowSpan(overlay, 3);
                        parentGrid.Children.Add(overlay);
                    }
                    else
                    {
                        parentGrid = new Grid();
                        parentWindow.Content = parentGrid;
                        parentGrid.Children.Add(new ContentPresenter { Content = parentWindow.Content });
                        parentGrid.Children.Add(overlay);
                    }
                }
                return _instances[parentWindow];
            }
        }
        public Window Window { get; set; }
        public Object parentContent { get; set; }


        public LoadingOverlay()
        {
            InitializeComponent();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {

        }
        public void Close()
        {
            lock (_lock)
            {
                _instances.Remove(Window);
                if (parentContent is Grid parentGrid)
                {
                    parentGrid.Children.Remove(this);
                }
                if (this.Parent is Panel panel)
                {
                    panel.Children.Remove(this);
                    if (this.Parent == parentContent)
                    {
                        Window.Content = parentContent;
                    }
                    Window.Content = parentContent;
                }

            }
        }


        public void Show(string message)
        {
            TextBoxMessage.Text = message;
            this.Visibility = Visibility.Visible;
        }
        public void UpdateMessage(string message)
        {
            TextBoxMessage.Text = message;
        }

        public void Hide()
        {
            this.Visibility = Visibility.Collapsed;
        }
    }
}
