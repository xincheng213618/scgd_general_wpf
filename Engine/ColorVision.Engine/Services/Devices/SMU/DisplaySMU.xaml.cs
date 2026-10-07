using ColorVision.Engine.Messages; // Added
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Views;
using ColorVision.Engine.Templates;
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
        public string PersistenceKey => Device.Config.Code;

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

            ComboxVITemplate.ItemsSource = TemplateSMUParam.Params;
            ComboxVITemplate.SelectedIndex = 0;

            CbChannel.ItemsSource = Enum.GetValues<SMUChannelType>();
            this.AddViewConfig(View, DisPlayName);
            this.ApplyChangedSelectedColor(DisPlayBorder);
        }

        private void DService_DeviceStatusChanged(object? sender, DeviceStatusType e)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => DService_DeviceStatusChanged(sender, e)); return; }
            void SetVisibility(UIElement element, Visibility visibility) { if (element.Visibility != visibility) element.Visibility = visibility; }
            void HideAllButtons()
            {
                SetVisibility(TextBlockUnknow, Visibility.Collapsed);
                SetVisibility(ButtonUnauthorized, Visibility.Collapsed);
                SetVisibility(StackPanelContent, Visibility.Collapsed);
                SetVisibility(TextBlockOffLine, Visibility.Collapsed);
                SetVisibility(StackPanelOpen, Visibility.Collapsed);
            }
            // Default state
            HideAllButtons();
            bool ready = e is DeviceStatusType.Opened or DeviceStatusType.LiveOpened or DeviceStatusType.Free;
            UseLocalSmuCheckBox.IsEnabled = Device.SmuBackend.CanSwitch;
            StackPanelOpen.IsEnabled = ready;
            ButtonSourceMeter1.IsEnabled = e is not (DeviceStatusType.Opening or DeviceStatusType.Closing)
                && (e != DeviceStatusType.Busy || Device.SmuBackend.LocalOwned);

            switch (e)
            {
                case DeviceStatusType.Unauthorized:
                    SetVisibility(ButtonUnauthorized, Visibility.Visible);
                    break;
                case DeviceStatusType.Unknown:
                    if (Device.SmuBackend.LocalOwned)
                    {
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        ButtonSourceMeter1.Content = ColorVision.Engine.Properties.Resources.Close;
                    }
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
                case DeviceStatusType.Free:
                case DeviceStatusType.Busy:
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

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            DService.DeviceStatusChanged -= DService_DeviceStatusChanged;
            DService.DeviceStatusChanged += DService_DeviceStatusChanged;
            DService_DeviceStatusChanged(sender, DService.DeviceStatus);
        }
        private void UserControl_Unloaded(object sender, RoutedEventArgs e) => DService.DeviceStatusChanged -= DService_DeviceStatusChanged;
        private void LocalSmuPreference_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            ConfigHandler.GetInstance().Save<DisplayConfigManager>();
            DService_DeviceStatusChanged(sender, DService.DeviceStatus);
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
                if (!Device.SmuBackend.LocalOwned && DService.DeviceStatus is not (DeviceStatusType.Opened or DeviceStatusType.LiveOpened or DeviceStatusType.Free))
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
            MsgRecord? msgRecord = DService.StepData(Device.DisplayConfig.IsSourceV, sourceConfig.MeasureVal, sourceConfig.LmtVal, Device.DisplayConfig.Channel);
            if (msgRecord != null && sender is Button button)
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
        private void MeasureDataClose_Click(object sender, RoutedEventArgs e)
        {
            bool local = Device.SmuBackend.OpensLocally;
            MsgRecord record = DService.CloseOutput();
            if (sender is Button button) ServicesHelper.SendTimedCommand(this, button, record, onTerminalStateChanged: (reply, state) =>
            {
                if (state == MsgRecordState.Fail) MessageBox.Show(Application.Current.GetActiveWindow(), reply.MsgReturn.Message, "ColorVision");
            });
            if (!local) { Device.DisplayConfig.V = null; Device.DisplayConfig.I = null; }
        }
        private void VIScan_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && ComboxVITemplate.SelectedItem is TemplateModel<SMUParam> templateModel)
            {
                EnsureTimedButtonOperations();
                bool local = Device.SmuBackend.OpensLocally;
                SMUChannelType channel = Device.DisplayConfig.Channel;
                MsgRecord msgRecord = DService.Scan(templateModel.Value, Device.DisplayConfig.Channel);
                if (msgRecord != null)
                {
                    ServicesHelper.SendTimedCommand(this, button, msgRecord, onTerminalStateChanged: async (record, state) =>
                    {
                        if (state == MsgRecordState.Success)
                        {
                            if (local) return;
                            if (record.MsgReturn.Code != 0)
                            {
                                DService.CloseOutput(channel);
                                MessageBox.Show($"GetData Eorr Code{record.MsgReturn.Code}");
                            }
                            else
                            {
                                log.Info("DelyaClose1000");
                                await Task.Delay(1000);
                                if (Device.SmuBackend.OpensLocally) return;
                                DService.CloseOutput(channel);
                                var channelConfig = channel == SMUChannelType.A ? Device.DisplayConfig.ChannelA : Device.DisplayConfig.ChannelB;
                                channelConfig.V = null;
                                channelConfig.I = null;
                                if (Device.DisplayConfig.Channel == channel) { Device.DisplayConfig.V = null; Device.DisplayConfig.I = null; }
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
            operations.Register(ButtonSourceMeter1, options =>
            {
                options.ContentFactory = stats => Device.SmuBackend.LocalOwned || DService.DeviceStatus is DeviceStatusType.Opened or DeviceStatusType.LiveOpened or DeviceStatusType.Free
                    ? ColorVision.Engine.Properties.Resources.Close
                    : TimedButtonOperationTextFormatter.BuildCompactContent(ColorVision.Engine.Properties.Resources.Open, stats);
                options.ToolTipFactory = stats => Device.SmuBackend.LocalOwned || DService.DeviceStatus is DeviceStatusType.Opened or DeviceStatusType.LiveOpened or DeviceStatusType.Free
                    ? Properties.Resources.CloseSourceMeter
                    : TimedButtonOperationTextFormatter.BuildTooltip(Properties.Resources.OpenSourceMeter, stats);
            });

            operations.Register(MeasureDataButton);
            operations.Register(StepMeasureDataButton);
            operations.Register(CloseOutputButton);

            operations.Register(VIScanButton);

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
