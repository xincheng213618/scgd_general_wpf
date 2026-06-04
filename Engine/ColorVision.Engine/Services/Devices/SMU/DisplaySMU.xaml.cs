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
            EnsureTimedButtonOperations();
            DService_DeviceStatusChanged(sender,DService.DeviceStatus);
            DService.DeviceStatusChanged += DService_DeviceStatusChanged;

            ComboxVITemplate.ItemsSource = TemplateSMUParam.Params;
            ComboxVITemplate.SelectedIndex = 0;

            CbChannel.ItemsSource = Enum.GetValues<SMUChannelType>();
            this.AddViewConfig(View, DisPlayName);
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

            this.TryGetTimedButtonOperations()?.RefreshIdleState(ButtonSourceMeter1);
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
                EnsureTimedButtonOperations();
                if (DService.DeviceStatus != DeviceStatusType.Opened)
                {
                    MsgRecord msgRecord = DService.Open(Config.IsNet, Config.DevName);
                    ServicesHelper.SendTimedCommand(this, button, msgRecord, onTerminalStateChanged: (record, state) =>
                    {
                        if (state == MsgRecordState.Fail)
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{record.MsgReturn.Message}", "ColorVision");
                        }
                    });

                }
                else
                {

                    MsgRecord msgRecord = DService.Close();
                    ServicesHelper.SendTimedCommand(this, button, msgRecord, onTerminalStateChanged: (record, state) =>
                    {
                        if (state == MsgRecordState.Fail)
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{record.MsgReturn.Message}", "ColorVision");
                        }
                    });
                }
            }
        }

        private void MeasureData_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                EnsureTimedButtonOperations();
                SMUSourceDisplayConfig sourceConfig = Device.DisplayConfig.CurrentSourceConfig;
                MsgRecord msgRecord = DService.GetData(Device.DisplayConfig.IsSourceV, sourceConfig.MeasureVal, sourceConfig.LmtVal, Device.DisplayConfig.Channel);
                if(msgRecord != null)
                {
                    ServicesHelper.SendTimedCommand(this, button, msgRecord, onTerminalStateChanged: (record, state) =>
                    {
                        if (state == MsgRecordState.Fail)
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{record.MsgReturn.Message}", "ColorVision");
                        }
                    });
                }
            }

        }
        private void StepMeasureData_Click(object sender, RoutedEventArgs e)
        {
            SMUSourceDisplayConfig sourceConfig = Device.DisplayConfig.CurrentSourceConfig;
            MsgRecord msgRecord = DService.GetData(Device.DisplayConfig.IsSourceV, sourceConfig.MeasureVal, sourceConfig.LmtVal, Device.DisplayConfig.Channel);
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
            if (sender is Button button && ComboxVITemplate.SelectedItem is TemplateModel<SMUParam> templateModel)
            {
                EnsureTimedButtonOperations();
                MsgRecord msgRecord = DService.Scan(templateModel.Value, Device.DisplayConfig.Channel);
                if (msgRecord != null)
                {
                    ServicesHelper.SendTimedCommand(this, button, msgRecord, onTerminalStateChanged: async (record, state) =>
                    {
                        if (state == MsgRecordState.Success)
                        {
                            if (record.MsgReturn.Code != 0)
                            {
                                DService.CloseOutput();
                                MessageBox.Show($"GetData Eorr Code{record.MsgReturn.Code}");
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
                            MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{record.MsgReturn.Message}", "ColorVision");
                        }
                    });
                }
            }

        }

        private TimedButtonOperationRegistry EnsureTimedButtonOperations()
        {
            TimedButtonOperationRegistry operations = this.GetTimedButtonOperations(BuildButtonOperationKey);
            operations.Register(
                ButtonSourceMeter1,
                "open-close",
                Properties.Resources.Open,
                Properties.Resources.SourceMeterSwitch,
                System.Windows.Media.Brushes.Red,
                contentFactory: stats => DService.DeviceStatus == DeviceStatusType.Opened
                    ? ColorVision.Engine.Properties.Resources.Close
                    : TimedButtonOperationTextFormatter.BuildCompactContent(ColorVision.Engine.Properties.Resources.Open, stats),
                tooltipFactory: stats => DService.DeviceStatus == DeviceStatusType.Opened
                    ? Properties.Resources.CloseSourceMeter
                    : TimedButtonOperationTextFormatter.BuildTooltip(Properties.Resources.OpenSourceMeter, stats));

            operations.Register(
                MeasureDataButton,
                "measure-data",
                Properties.Resources.Ignite,
                Properties.Resources.IgniteMeasurement,
                System.Windows.Media.Brushes.Red);

            operations.Register(
                VIScanButton,
                "vi-scan",
                Properties.Resources.Scan,
                Properties.Resources.VIScan,
                System.Windows.Media.Brushes.Red);

            return operations;
        }

        private string BuildButtonOperationKey(string actionKey)
        {
            return $"smu:{Device.Config.Code}:{actionKey}";
        }


        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                TemplateEditorWindow windowTemplate;
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
