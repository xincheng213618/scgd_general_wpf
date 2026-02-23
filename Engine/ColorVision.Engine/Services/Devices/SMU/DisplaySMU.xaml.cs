using ColorVision.Database;
using ColorVision.Engine.Messages; // Added
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Views;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using log4net;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Services.Devices.SMU
{
    /// <summary>
    /// DisplaySMU.xaml 的交互逻辑
    /// </summary>
    public partial class DisplaySMU : UserControl, IDisPlayControl
    {

        private static readonly ILog log = LogManager.GetLogger(typeof(DisplaySMU));

        public DeviceSMU Device { get; set; }
        private MQTTSMU DService { get => Device.DService;  }
        private ConfigSMU Config { get => Device.Config; }

        public ViewSMU View { get => Device.View; }

        public string DisPlayName => Device.Config.Name;

        public DisplaySMU(DeviceSMU deviceSMU)
        {
            Device = deviceSMU;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;

            // When switching between voltage and current modes, swap the source and limit values so that the numbers follow the semantic meaning instead of the textbox position
            if (Device.DisplayConfig is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, ev) =>
                {
                    if (ev.PropertyName == nameof(Device.DisplayConfig.IsSourceV))
                    {
                        // Swap MeasureVal (source) and LmtVal (limit)
                        double oldMeasure = Device.DisplayConfig.MeasureVal;
                        Device.DisplayConfig.MeasureVal = Device.DisplayConfig.LmtVal;
                        Device.DisplayConfig.LmtVal = oldMeasure;
                    }
                };
            }
            DService_DeviceStatusChanged(sender,DService.DeviceStatus);
            DService.DeviceStatusChanged += DService_DeviceStatusChanged;

            ComboxVITemplate.ItemsSource = TemplateSMUParam.Params;
            ComboxVITemplate.SelectionChanged += (s, e) =>
            {
                if (ComboxVITemplate.SelectedItem is TemplateModel<SMUParam> KeyValue && KeyValue.Value is SMUParam SxParm)
                {
                    Device.DisplayConfig.StartMeasureVal = SxParm.StartMeasureVal;
                    Device.DisplayConfig.StopMeasureVal = SxParm.StopMeasureVal;
                    Device.DisplayConfig.IsSourceV = SxParm.IsSourceV;
                    Device.DisplayConfig.LimitVal = SxParm.LmtVal;
                    Device.DisplayConfig.Number = SxParm.Number;
                }
            };
            ComboxVITemplate.SelectedIndex = 0;

            CbChannel.ItemsSource = Enum.GetValues(typeof(SMUChannelType));
            this.AddViewConfig(View, ComboxView);
            this.ApplyChangedSelectedColor(DisPlayBorder);
        }

        private void DService_DeviceStatusChanged(object? sender, DeviceStatusType e)
        {
            void SetVisibility(UIElement element, Visibility visibility) { if (element.Visibility != visibility) element.Visibility = visibility; }
            void HideAllButtons()
            {
                SetVisibility(TextBlockUnknow, Visibility.Collapsed);
                SetVisibility(ButtonUnauthorized, Visibility.Collapsed);
                SetVisibility(StackPanelContent, Visibility.Collapsed);
                SetVisibility(TextBlockOffLine, Visibility.Collapsed);
            }
            // Default state
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
                    SetVisibility(TextBlockOffLine, Visibility.Visible);
                    break;
                case DeviceStatusType.UnInit:
                    SetVisibility(StackPanelContent, Visibility.Visible);
                    break;
                case DeviceStatusType.Closed:
                    SetVisibility(StackPanelContent, Visibility.Visible);
                    ButtonSourceMeter1.Content = ColorVision.Engine.Properties.Resources.Open;
                    break;
                case DeviceStatusType.LiveOpened:
                case DeviceStatusType.Opened:
                    SetVisibility(StackPanelOpen, Visibility.Visible);
                    SetVisibility(StackPanelContent, Visibility.Visible);
                    ButtonSourceMeter1.Content = ColorVision.Engine.Properties.Resources.Close;
                    break;
                case DeviceStatusType.Closing:
                    SetVisibility(StackPanelContent, Visibility.Visible);
                    ButtonSourceMeter1.Content = ColorVision.Engine.Properties.Resources.Closing;
                    break;
                case DeviceStatusType.Opening:
                    SetVisibility(StackPanelContent, Visibility.Visible);
                    ButtonSourceMeter1.Content = ColorVision.Engine.Properties.Resources.Opening;
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

        private void ButtonSourceMeter1_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (DService.DeviceStatus != DeviceStatusType.Opened)
                {
                    MsgRecord msgRecord = DService.Open(Config.IsNet, Config.DevName);
                    ServicesHelper.SendCommand(button, msgRecord);
                    msgRecord.MsgRecordStateChanged += (s,e) =>
                    {
                        if (e == MsgRecordState.Fail)
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{msgRecord.MsgReturn.Message}", "ColorVision");
                        }
                    };

                }
                else
                {

                    MsgRecord msgRecord = DService.Close();
                    ServicesHelper.SendCommand(button, DService.Close());
                    msgRecord.MsgRecordStateChanged += (s,e) =>
                    {
                        if (e == MsgRecordState.Fail)
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{msgRecord.MsgReturn.Message}", "ColorVision");
                        }
                    };
                }
            }
        }

        private void MeasureData_Click(object sender, RoutedEventArgs e)
        {
            MsgRecord msgRecord = DService.GetData(Device.DisplayConfig.IsSourceV, Device.DisplayConfig.MeasureVal, Device.DisplayConfig.LmtVal, Device.DisplayConfig.Channel);
            if(msgRecord != null)
            {
                msgRecord.MsgRecordStateChanged += (s,e) =>
                {
                    if (e == MsgRecordState.Fail)
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{msgRecord.MsgReturn.Message}", "ColorVision");
                    }
                };
            }

        }
        private void StepMeasureData_Click(object sender, RoutedEventArgs e)
        {
            MsgRecord msgRecord = DService.GetData(Device.DisplayConfig.IsSourceV, Device.DisplayConfig.MeasureVal, Device.DisplayConfig.LmtVal, Device.DisplayConfig.Channel);
            if (msgRecord != null)
            {
                msgRecord.MsgRecordStateChanged += (s,e) =>
                {
                    if (e == MsgRecordState.Fail)
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{msgRecord.MsgReturn.Message}", "ColorVision");
                    }
                };
            }

        }
        private void MeasureDataClose_Click(object sender, RoutedEventArgs e)
        {
            DService.CloseOutput();
            Device.DisplayConfig.V = null;
            Device.DisplayConfig.I = null;
        }
        private void VIScan_Click(object sender, RoutedEventArgs e)
        {
            MsgRecord msgRecord = DService.Scan(Device.DisplayConfig.IsSourceV, Device.DisplayConfig.StartMeasureVal, Device.DisplayConfig.StopMeasureVal, Device.DisplayConfig.LimitVal, Device.DisplayConfig.Number, Device.DisplayConfig.Channel);
            if (msgRecord != null)
            {
                msgRecord.MsgRecordStateChanged += async (s,e) =>
                {
                    if (e == MsgRecordState.Success)
                    {
                        if (msgRecord.MsgReturn.Code != 0)
                        {
                            DService.CloseOutput();
                            MessageBox.Show($"GetData Eorr Code{msgRecord.MsgReturn.Code}");
                        }
                        else
                        {
                            log.Info("DelyaClose1000");
                            await Task.Delay(1000);
                            DService.CloseOutput();
                            Device.DisplayConfig.V = null;
                            Device.DisplayConfig.I = null;
                            log.Info("DelyaClose1000 1");
                            await Task.Delay(1000);
                        }
                    }
                    else
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{msgRecord.MsgReturn.Message}", "ColorVision");
                    }
                };
            }

        }


        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ToggleButton0.IsChecked = !ToggleButton0.IsChecked;
        }

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                TemplateEditorWindow windowTemplate;
                if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
                {
                    MessageBox1.Show(Properties.Resources.DatabaseConnectionFailed, "ColorVision");
                    return;
                }
                switch (control.Tag?.ToString() ?? string.Empty)
                {

                    case "SMUParam":
                        windowTemplate = new TemplateEditorWindow(new TemplateSMUParam());
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                }
            }
        }
    }
}
