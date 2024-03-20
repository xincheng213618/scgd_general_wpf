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



        private void Button_New_Click(object sender, RoutedEventArgs e)
        {
            MQTTCreate.Visibility = Visibility.Collapsed;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            MQTTCreate.Visibility = MQTTCreate.Visibility == Visibility.Visible? Visibility.Collapsed : Visibility.Visible;
            if (ServiceTerminal.MQTTServiceTerminalBase is MQTTServiceTerminalBase baseServiceBase)
            {
                TextBox_Code.ItemsSource = baseServiceBase.DevicesSN;
                TextBox_Name.ItemsSource = baseServiceBase.DevicesSN;
            }
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


        private void TextBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            popup.IsOpen = true;
        }

        private void popup_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Popup popup)
                popup.IsOpen = false;

        }
    }
}
