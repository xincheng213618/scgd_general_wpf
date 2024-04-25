using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Services.Devices.CfwPort
{
    /// <summary>
    /// DisplaySMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayCfwPortControl : UserControl,IDisPlayControl
    {

        public DeviceCfwPort Device { get; set; }
        private MQTTCfwPort DeviceService { get => Device.DeviceService;  }

        public DisplayCfwPortControl(DeviceCfwPort device)
        {
            Device = device;
            InitializeComponent();
            PreviewMouseDown += UserControl_PreviewMouseDown;

        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            SelectChanged += (s, e) =>
            {
                DisPlayBorder.BorderBrush = IsSelected ? ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#5649B0" : "#A79CF1") : ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#EAEAEA" : "#151515");
            };
            ThemeManager.Current.CurrentUIThemeChanged += (s) =>
            {
                DisPlayBorder.BorderBrush = IsSelected ? ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#5649B0" : "#A79CF1") : ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#EAEAEA" : "#151515");
            };
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }


        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Parent is StackPanel stackPanel)
            {
                if (stackPanel.Tag is IDisPlayControl disPlayControl)
                    disPlayControl.IsSelected = false;
                stackPanel.Tag = this;
                IsSelected = true;
            }
        }

        private void GetPort_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DeviceService.GetPort();
                ServicesHelper.SendCommand(button, msgRecord);
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DeviceService.Open();
                ServicesHelper.SendCommand(button, msgRecord);
            }

        }

        private void SetPort_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (int.TryParse(TextPort.Text,out int port))
                {
                    var msgRecord = DeviceService.SetPort(port);
                    ServicesHelper.SendCommand(button, msgRecord);
                }
            }
        }

        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ToggleButton0.IsChecked = !ToggleButton0.IsChecked;
        }
    }
}
