using ColorVision.Services.Devices;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;


namespace ColorVision.Services.Terminal
{
    /// <summary>
    /// TerminalServiceControl.xaml 的交互逻辑
    /// </summary>
    public partial class TerminalServiceControl : UserControl
    {
        public TerminalService ServiceTerminal { get; set; }  

        public TerminalServiceControl(TerminalService mQTTService)
        {
            this.ServiceTerminal = mQTTService;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ServiceTerminal;

            if (ServiceTerminal.VisualChildren.Count == 0)
                ListViewService.Visibility = Visibility.Collapsed;
            ListViewService.ItemsSource = ServiceTerminal.VisualChildren;

            ServiceTerminal.VisualChildren.CollectionChanged += (s, e) =>
            {
                if (ServiceTerminal.VisualChildren.Count == 0)
                {
                    ListViewService.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ListViewService.Visibility = Visibility.Visible;
                }
            };
        }

        private void ListViewService_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                if (ServiceTerminal.VisualChildren[listView.SelectedIndex] is DeviceService baseObject)
                {
                    if (this.Parent is Grid grid)
                    {
                        grid.Children.Clear();
                        grid.Children.Add(baseObject.GetDeviceControl());
                    }

                }
            }
        }

    }
}
