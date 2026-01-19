using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Scheduler;
using ColorVision.UI;
using Quartz;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Services.Devices.Spectrum
{
    [DisplayName("光谱仪单次测试")]
    public class SpectrumGetDataJob : IJob
    {


        public  Task Execute(IJobExecutionContext context)
        {
            var schedulerInfo = QuartzSchedulerManager.GetInstance().TaskInfos.First(x => x.JobName == context.JobDetail.Key.Name && x.GroupName == context.JobDetail.Key.Group);
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                schedulerInfo.Status = SchedulerStatus.Running;
            });

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                DeviceSpectrum deviceSpectrum = ServiceManager.GetInstance().DeviceServices.OfType<DeviceSpectrum>().LastOrDefault();
                deviceSpectrum?.DService.GetData();
                schedulerInfo.Status = SchedulerStatus.Ready;
            });
            return Task.CompletedTask;
        }
    }


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

            //增加进度显示
            Device.SelfAdaptionInitDarkStarted += () =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Overlay.Visibility=Visibility.Visible;
                    LoadingPanel.Visibility=Visibility.Visible;
                });
            };
            Device.SelfAdaptionInitDarkCompleted += () =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Overlay.Visibility = Visibility.Collapsed;
                    LoadingPanel.Visibility = Visibility.Collapsed;
                });
            };
            this.AddViewConfig(View,ComboxView);
            this.ContextMenu = Device.ContextMenu;


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
                btn_autoTest.Content = ColorVision.Engine.Properties.Resources.ContinuousMeasurement;
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

        private void Device_SelfAdaptionInitDarkStarted()
        {
            throw new NotImplementedException();
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
                    MsgRecord msgRecord = DService.Open();

                }
                else
                {
                    btn_connect.Content = ColorVision.Engine.Properties.Resources.Closing;
                    MsgRecord msgRecord = DService.Close();
                }
            }
        }
        private void Button_Click_OneTest(object sender, RoutedEventArgs e)
        {
            MsgRecord msgRecord = DService.GetData();
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
            if (!string.IsNullOrWhiteSpace(btnTitle) && btnTitle.Equals(ColorVision.Engine.Properties.Resources.ContinuousMeasurement, StringComparison.Ordinal))
            {
                DService.GetDataAuto();
                btn_autoTest.Content = ColorVision.Engine.Properties.Resources.CancelAutoTest;
            }
            else
            {
                DService.GetDataAutoStop();
                btn_autoTest.Content = ColorVision.Engine.Properties.Resources.ContinuousMeasurement;
            }
        }
        private void Button_Click_Init_Dark(object sender, RoutedEventArgs e)
        {
            MsgRecord  msgRecord = DService.InitDark();
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

        private void GetNDport_Click(object sender, RoutedEventArgs e)
        {
            MsgRecord msgRecord = DService.GetPort();
            msgRecord.MsgRecordStateChanged += (e) =>
            {
                if (e == MsgRecordState.Success)
                {
                    int port = msgRecord.MsgReturn.Data.Port;
                    Device.DisplayConfig.PortNum = port;
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ExecutionFailed, "ColorVision");
                }
            };
        }

        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ToggleButton0.IsChecked = !ToggleButton0.IsChecked;
        }
    }
}
