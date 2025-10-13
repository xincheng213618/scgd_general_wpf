using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.UI;
using CVCommCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Services.Devices.Spectrum
{
    /// <summary>
    /// DisplaySpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class DisplaySpectrum : UserControl, IDisPlayControl, IStatusBarInfoProvider
    {
        public DeviceSpectrum Device { get; set; }
        public MQTTSpectrum SpectrumService { get => Device.DService; }

        public ViewSpectrum View { get => Device.View;}

        public string DisPlayName => Device.Config.Name;

        public DisplaySpectrum(DeviceSpectrum DeviceSpectrum)
        {
            this.Device = DeviceSpectrum;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;

            this.AddViewConfig(View,ComboxView);

            this.ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Property, Command = Device.PropertyCommand });

            void UpdateUI(DeviceStatusType status)
            {
                void SetVisibility(UIElement element, Visibility visibility) { if (element.Visibility != visibility) element.Visibility = visibility; };

                void HideAllButtons()
                {
                    SetVisibility(ButtonUnauthorized, Visibility.Collapsed);
                    SetVisibility(TextBlockUnknow, Visibility.Collapsed);
                    SetVisibility(StackPanelContent, Visibility.Collapsed);
                    SetVisibility(StackPanelOpen, Visibility.Collapsed);
                    SetVisibility(TextBlockOffLine, Visibility.Collapsed);
                }

                HideAllButtons();
                btn_autoTest.Content = "自动测试";
                switch (status)
                {

                    case DeviceStatusType.Unknown:
                        SetVisibility(TextBlockUnknow, Visibility.Visible);
                        break;
                    case DeviceStatusType.Unauthorized:
                        SetVisibility(ButtonUnauthorized, Visibility.Visible);
                        break;
                    case DeviceStatusType.OffLine:
                        SetVisibility(TextBlockOffLine, Visibility.Visible);
                        break;
                    case DeviceStatusType.UnInit:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        btn_connect.Content = "打开";
                        break;
                    case DeviceStatusType.Closed:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        btn_connect.Content = "打开";
                        break;
                    case DeviceStatusType.Opened:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        SetVisibility(StackPanelOpen, Visibility.Visible);
                        btn_connect.Content = "关闭";
                        break;
                    case DeviceStatusType.SP_Continuous_Mode:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        SetVisibility(StackPanelOpen, Visibility.Visible);
                        btn_autoTest.Content = "取消自动测试";
                        break;
                    default:
                        break;
                }
            }

            UpdateUI(SpectrumService.DeviceStatus);
            SpectrumService.DeviceStatusChanged += UpdateUI;

            this.ApplyChangedSelectedColor(DisPlayBorder);
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }




        #region MQTT

        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {
            string btnTitle = btn_connect.Content.ToString();
            if (!string.IsNullOrWhiteSpace(btnTitle))
            {
                if (!btnTitle.Equals("关闭", StringComparison.Ordinal))
                {
                    btn_connect.Content = "打开中";
                    SpectrumService.Open();
                }
                else
                {
                    btn_connect.Content = "关闭中";
                    SpectrumService.Close();
                }
            }
        }
        private void Button_Click_OneTest(object sender, RoutedEventArgs e)
        {
            MsgRecord msgRecord = SpectrumService.GetData((float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value, AutoIntTime.IsChecked??false, AutoDark.IsChecked ?? false, AutoShutterDark.IsChecked ?? false);
            msgRecord.MsgRecordStateChanged += (e) =>
            {
                if (e == MsgRecordState.Success)
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "执行结束", "ColorVision");
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "执行失败", "ColorVision");
                }
            };
        }

        private void Button_Click_AutoTest(object sender, RoutedEventArgs e)
        {
            string btnTitle = btn_autoTest.Content.ToString();
            if (!string.IsNullOrWhiteSpace(btnTitle) && btnTitle.Equals("自动测试", StringComparison.Ordinal))
            {
                SpectrumService.GetDataAuto((float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value, AutoIntTime.IsChecked ?? false, AutoDark.IsChecked ?? false);
                btn_autoTest.Content = "取消自动测试";
            }
            else
            {
                SpectrumService.GetDataAutoStop();
                btn_autoTest.Content = "自动测试";
            }
        }
        private void Button_Click_Init_Dark(object sender, RoutedEventArgs e)
        {
            MsgRecord  msgRecord = SpectrumService.InitDark((float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value);
            msgRecord.MsgRecordStateChanged += (e) =>
            {
                if (e == MsgRecordState.Success)
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "执行结束", "ColorVision");
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "执行失败", "ColorVision");
                }
            };
        }

        #endregion

        private void Button_Click_Shutter_Connect(object sender, RoutedEventArgs e)
        {
            SpectrumService.ShutterConnect();
        }

        private void Button_Click_Shutter_Doopen(object sender, RoutedEventArgs e)
        {
            SpectrumService.ShutterDoopen();
        }

        private void Button_Click_Shutter_Doclose(object sender, RoutedEventArgs e)
        {
            SpectrumService.ShutterDoclose();
        }

        #region IStatusBarInfoProvider Implementation

        private ObservableCollection<StatusBarInfoItem> _statusBarInfo;

        /// <summary>
        /// 获取状态栏信息
        /// 示例实现：显示设备名称、状态和通道信息
        /// </summary>
        public ObservableCollection<StatusBarInfoItem> GetStatusBarInfo()
        {
            if (_statusBarInfo == null)
            {
                _statusBarInfo = new ObservableCollection<StatusBarInfoItem>
                {
                    new StatusBarInfoItem
                    {
                        Key = "DeviceName",
                        Label = "设备:",
                        Value = Device.Config.Name,
                        Order = 0,
                        IsVisible = true
                    },
                    new StatusBarInfoItem
                    {
                        Key = "DeviceStatus",
                        Label = "状态:",
                        Value = GetStatusText(SpectrumService.DeviceStatus),
                        Order = 1,
                        IsVisible = true
                    },
                    new StatusBarInfoItem
                    {
                        Key = "IntegrationTime",
                        Label = "积分时间:",
                        Value = $"{SpectrumSliderIntTime?.Value ?? 0:F2}ms",
                        Order = 2,
                        IsVisible = true
                    },
                    new StatusBarInfoItem
                    {
                        Key = "AverageNumber",
                        Label = "平均次数:",
                        Value = $"{SpectrumSliderAveNum?.Value ?? 0}",
                        Order = 3,
                        IsVisible = true
                    }
                };

                // 订阅设备状态变化事件
                SpectrumService.DeviceStatusChanged += (status) =>
                {
                    var statusItem = _statusBarInfo.FirstOrDefault(item => item.Key == "DeviceStatus");
                    if (statusItem != null)
                    {
                        statusItem.Value = GetStatusText(status);
                    }
                };

                // 订阅滑块值变化事件
                if (SpectrumSliderIntTime != null)
                {
                    SpectrumSliderIntTime.ValueChanged += (s, e) =>
                    {
                        var item = _statusBarInfo.FirstOrDefault(i => i.Key == "IntegrationTime");
                        if (item != null)
                        {
                            item.Value = $"{e.NewValue:F2}ms";
                        }
                    };
                }

                if (SpectrumSliderAveNum != null)
                {
                    SpectrumSliderAveNum.ValueChanged += (s, e) =>
                    {
                        var item = _statusBarInfo.FirstOrDefault(i => i.Key == "AverageNumber");
                        if (item != null)
                        {
                            item.Value = $"{e.NewValue}";
                        }
                    };
                }
            }

            return _statusBarInfo;
        }

        /// <summary>
        /// 将设备状态转换为可读文本
        /// </summary>
        private string GetStatusText(DeviceStatusType status)
        {
            return status switch
            {
                DeviceStatusType.Unknown => "未知",
                DeviceStatusType.Unauthorized => "未授权",
                DeviceStatusType.OffLine => "离线",
                DeviceStatusType.UnInit => "未初始化",
                DeviceStatusType.Closed => "已关闭",
                DeviceStatusType.Opened => "已打开",
                DeviceStatusType.SP_Continuous_Mode => "连续模式",
                _ => status.ToString()
            };
        }

        #endregion
    }
}
