using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.UI;
using CVCommCore;
using System;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Services.Devices.Spectrum
{
    /// <summary>
    /// DisplaySpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class DisplaySpectrum : UserControl, IDisPlayControl
    {
        public DeviceSpectrum Device { get; set; }
        public MQTTSpectrum DService { get => Device.DService; }

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
                btn_autoTest.Content = ColorVision.Engine.Properties.Resources.AutoTest;
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
                        btn_connect.Content = ColorVision.Engine.Properties.Resources.Open;
                        break;
                    case DeviceStatusType.Closed:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        btn_connect.Content = ColorVision.Engine.Properties.Resources.Open;
                        break;
                    case DeviceStatusType.Opened:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        SetVisibility(StackPanelOpen, Visibility.Visible);
                        btn_connect.Content = ColorVision.Engine.Properties.Resources.Close;
                        break;
                    case DeviceStatusType.SP_Continuous_Mode:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        SetVisibility(StackPanelOpen, Visibility.Visible);
                        btn_autoTest.Content = ColorVision.Engine.Properties.Resources.CancelAutoTest;
                        break;
                    default:
                        break;
                }
            }

            UpdateUI(DService.DeviceStatus);
            DService.DeviceStatusChanged += UpdateUI;

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
                if (!btnTitle.Equals(ColorVision.Engine.Properties.Resources.Close, StringComparison.Ordinal))
                {
                    btn_connect.Content = ColorVision.Engine.Properties.Resources.Opening;
                    DService.Open();
                }
                else
                {
                    btn_connect.Content = ColorVision.Engine.Properties.Resources.Closing;
                    DService.Close();
                }
            }
        }
        private void Button_Click_OneTest(object sender, RoutedEventArgs e)
        {
            MsgRecord msgRecord = DService.GetData((float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value, AutoIntTime.IsChecked??false, AutoDark.IsChecked ?? false, AutoShutterDark.IsChecked ?? false);
            msgRecord.MsgRecordStateChanged += (e) =>
            {
                if (e == MsgRecordState.Success)
                {
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ExecutionFailed, "ColorVision");
                }
            };
        }

        private void Button_Click_AutoTest(object sender, RoutedEventArgs e)
        {
            string btnTitle = btn_autoTest.Content.ToString();
            if (!string.IsNullOrWhiteSpace(btnTitle) && btnTitle.Equals(ColorVision.Engine.Properties.Resources.AutoTest, StringComparison.Ordinal))
            {
                DService.GetDataAuto((float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value, AutoIntTime.IsChecked ?? false, AutoDark.IsChecked ?? false);
                btn_autoTest.Content = ColorVision.Engine.Properties.Resources.CancelAutoTest;
            }
            else
            {
                DService.GetDataAutoStop();
                btn_autoTest.Content = ColorVision.Engine.Properties.Resources.AutoTest;
            }
        }
        private void Button_Click_Init_Dark(object sender, RoutedEventArgs e)
        {
            MsgRecord  msgRecord = DService.InitDark((float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value);
            msgRecord.MsgRecordStateChanged += (e) =>
            {
                if (e == MsgRecordState.Success)
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ExecutionComplete, "ColorVision");
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ExecutionFailed, "ColorVision");
                }
            };
        }

        #endregion

        private void Button_Click_Shutter_Connect(object sender, RoutedEventArgs e)
        {
            DService.ShutterConnect();
        }

        private void Button_Click_Shutter_Doopen(object sender, RoutedEventArgs e)
        {
            DService.ShutterDoopen();
        }

        private void Button_Click_Shutter_Doclose(object sender, RoutedEventArgs e)
        {
            DService.ShutterDoclose();
        }

        private void NDport_Click(object sender, RoutedEventArgs e)
        {
            MsgRecord msgRecord = DService.SetPort();
            msgRecord.MsgRecordStateChanged += (e) =>
            {
                if (e == MsgRecordState.Success)
                {
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ExecutionFailed, "ColorVision");
                }
            };
        }
    }
}
