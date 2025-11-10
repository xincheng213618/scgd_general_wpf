using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.UI;
using CVCommCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.CfwPort
{

    public class HoleMap:ViewModelBase
    {
        public int HoleIndex { get => _HoleIndex; set { _HoleIndex = value; OnPropertyChanged(); } }
        private int _HoleIndex;

        public string HoldName { get => _HoldName; set { _HoldName = value; OnPropertyChanged(); } }
        private string _HoldName;
    }

    public class FilterWheelConfig: ViewModelBase
    {
        public int HoleNum { get => _HoleNum; set { _HoleNum = value; OnPropertyChanged(); } }
        private int _HoleNum;

        public ObservableCollection<HoleMap> HoleMapping { get => _HoleMapping; set { _HoleMapping = value; OnPropertyChanged(); } }
        private ObservableCollection<HoleMap> _HoleMapping = new ObservableCollection<HoleMap>();
    }



    /// <summary>
    /// DisplaySMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayCfwPort : UserControl,IDisPlayControl
    {

        public DeviceCfwPort Device { get; set; }
        private MQTTCfwPort DService { get => Device.DService;  }

        public string DisPlayName => Device.Config.Code;

        public DisplayCfwPort(DeviceCfwPort device)
        {
            Device = device;
            InitializeComponent();
        }
        FilterWheelConfig filterWheelConfig = new FilterWheelConfig();
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            this.ApplyChangedSelectedColor(DisPlayBorder);
            List<int> ports = new List<int>();
            filterWheelConfig.HoleNum = 5;
            filterWheelConfig.HoleMapping.Add(new HoleMap() { HoleIndex = 0, HoldName = "ND0" });
            filterWheelConfig.HoleMapping.Add(new HoleMap() { HoleIndex = 1, HoldName = "ND10" });
            filterWheelConfig.HoleMapping.Add(new HoleMap() { HoleIndex = 2, HoldName = "ND100" });
            filterWheelConfig.HoleMapping.Add(new HoleMap() { HoleIndex = 3, HoldName = "ND1000" });
            filterWheelConfig.HoleMapping.Add(new HoleMap() { HoleIndex = 4, HoldName = "EMPTY" });


            CombPort.ItemsSource = filterWheelConfig.HoleMapping;
            CombPort.DisplayMemberPath ="HoldName";

            void UpdateUI(DeviceStatusType status)
            {
                void SetVisibility(UIElement element, Visibility visibility) { if (element.Visibility != visibility) element.Visibility = visibility; }
                ;
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

                switch (status)
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
            UpdateUI(DService.DeviceStatus);
            DService.DeviceStatusChanged += UpdateUI;


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
                ServicesHelper.SendCommand(button, msgRecord);
            }

        }
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DService.Clsoe();
                ServicesHelper.SendCommand(button, msgRecord);
            }
        }
        private void SetPort_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (int.TryParse(CombPort.Text,out int port))
                {
                    var msgRecord = DService.SetPort(port);
                    msgRecord.MsgRecordStateChanged += (e) =>
                    {
                        if (e == MsgRecordState.Success)
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.Success, "ColorVision");
                        }
                        else
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.执行失败, "ColorVision");
                        }
                    };

                }
            }
        }

        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ToggleButton0.IsChecked = !ToggleButton0.IsChecked;
        }

        private void CameraOffline_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DService.Open();
                ServicesHelper.SendCommand(button, msgRecord);
            }
        }

        private void GetPort_Click(object sender, RoutedEventArgs e)
        {
            MsgRecord msgRecord = DService.GetPort();
            msgRecord.MsgRecordStateChanged += (e) =>
            {
                if (e == MsgRecordState.Success)
                {
                    int port = msgRecord.MsgReturn.Data.Port;
                    CombPort.SelectedIndex = port;
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ExecutionFailed, "ColorVision");
                }
            };

        }
    }
}
