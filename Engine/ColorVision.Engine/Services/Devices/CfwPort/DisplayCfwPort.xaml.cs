#pragma warning disable CA1816
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.UI;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.CfwPort
{
    /// <summary>
    /// DisplayCfwPort.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayCfwPort : UserControl, IDisPlayControl, IDisposable
    {

        public DeviceCfwPort Device { get; set; }
        private MQTTCfwPort DService { get => Device.DService; }

        public string DisPlayName => Device.Config.Code;
        public string PersistenceKey => Device.Config.Code;

        public DisplayCfwPort(DeviceCfwPort device)
        {
            Device = device;
            InitializeComponent();
        }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            this.ApplyChangedSelectedColor(DisPlayBorder);
            RefreshPortMappings();
            Device.ConfigChanged += Device_ConfigChanged;

            DService_DeviceStatusChanged(sender, DService.DeviceStatus);
            DService.DeviceStatusChanged += DService_DeviceStatusChanged; ;


        }

        private void Device_ConfigChanged(object? sender, EventArgs e) => RefreshPortMappings();

        private void RefreshPortMappings()
        {
            int? port = Device.DisplayConfig.PortNum;
            CombPort.ItemsSource = Device.FilterWheelConfig.HoleMapping;
            CombPort.DisplayMemberPath = "HoleName";
            CombPort.SetCurrentValue(ComboBox.SelectedValueProperty, port);
        }

        private void DService_DeviceStatusChanged(object? sender, DeviceStatusType e)
        {
            void SetVisibility(UIElement element, Visibility visibility) { if (element.Visibility != visibility) element.Visibility = visibility; }
            void HideAllButtons()
            {
                SetVisibility(ButtonOpen, Visibility.Collapsed);
                SetVisibility(ButtonClose, Visibility.Collapsed);
                SetVisibility(ButtonInit, Visibility.Collapsed);
                SetVisibility(ButtonOffline, Visibility.Collapsed);
                SetVisibility(ButtonUnauthorized, Visibility.Collapsed);
                SetVisibility(TextBlockUnknow, Visibility.Collapsed);
                SetVisibility(StackPanelOpen, Visibility.Collapsed);
            }
            HideAllButtons();

            switch (e)
            {
                case DeviceStatusType.Unauthorized:
                    SetVisibility(ButtonUnauthorized, Visibility.Visible);
                    break;
                case DeviceStatusType.Unknown:
                    SetVisibility(TextBlockUnknow, Visibility.Visible);
                    break;
                case DeviceStatusType.OffLine:
                    SetVisibility(ButtonOffline, Visibility.Visible);
                    break;
                case DeviceStatusType.UnInit:
                    SetVisibility(ButtonInit, Visibility.Visible);
                    break;
                case DeviceStatusType.Closing:
                case DeviceStatusType.Closed:
                    SetVisibility(ButtonOpen, Visibility.Visible);
                    break;
                case DeviceStatusType.LiveOpened:
                case DeviceStatusType.Opening:
                case DeviceStatusType.Opened:
                    SetVisibility(StackPanelOpen, Visibility.Visible);
                    SetVisibility(ButtonClose, Visibility.Visible);
                    break;
                default:
                    break;
            }
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }


        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DService.Open();
                msgRecord.MsgRecordStateChanged += (s, e) =>
                {
                    if (e == MsgRecordState.Fail)
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{msgRecord.MsgReturn.Message}", "ColorVision");
                    }
                };
            }

        }
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DService.Clsoe();
                msgRecord.MsgRecordStateChanged += (s, e) =>
                {
                    if (e == MsgRecordState.Fail)
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{msgRecord.MsgReturn.Message}", "ColorVision");
                    }
                };
            }
        }
        private void SetPort_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && CombPort.SelectedItem is HoleMap holeMap)
            {
                var msgRecord = DService.SetPort(holeMap.HoleIndex);
                msgRecord.MsgRecordStateChanged += (s, e) =>
                {
                    if (e == MsgRecordState.Success)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ApplyBoundSpectrumCalibration(holeMap.HoleIndex, true);
                            MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.Success, "ColorVision");
                        });
                    }
                    else
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{msgRecord.MsgReturn.Message}", "ColorVision");
                    }
                };
            }
        }

        private void CameraOffline_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DService.Open();
                msgRecord.MsgRecordStateChanged += (s, e) =>
                {
                    if (e == MsgRecordState.Fail)
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{msgRecord.MsgReturn.Message}", "ColorVision");
                    }
                };
            }
        }

        private void GetPort_Click(object sender, RoutedEventArgs e)
        {
            MsgRecord msgRecord = DService.GetPort();
            msgRecord.MsgRecordStateChanged += (s, e) =>
            {
                if (e == MsgRecordState.Success)
                {
                    int port = msgRecord.MsgReturn.Data.Port;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Device.DisplayConfig.PortNum = port;
                        ApplyBoundSpectrumCalibration(port, false);
                    });
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{msgRecord.MsgReturn.Message}", "ColorVision");
                }
            };

        }

        public void Dispose()
        {
            Device.ConfigChanged -= Device_ConfigChanged;
            DService.DeviceStatusChanged -= DService_DeviceStatusChanged;
        }

        private void ApplyBoundSpectrumCalibration(int holeIndex, bool forceRestart)
        {
            foreach (var spectrum in ServiceManager.GetInstance().DeviceServices.OfType<DeviceSpectrum>())
            {
                if (!string.Equals(spectrum.Config.NDConfig.NDBindDeviceCode, Device.Code, StringComparison.Ordinal))
                    continue;

                spectrum.DisplayConfig.PortNum = holeIndex;
                spectrum.ApplyCalibrationGroupForND(holeIndex, forceRestart);
            }
        }
    }
}
