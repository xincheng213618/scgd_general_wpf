#pragma warning disable CA1816
using ColorVision.Themes.Controls;
using ColorVision.UI;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.PG
{
    /// <summary>
    /// DisplayPG.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayPG : UserControl, IDisPlayControl,IDisposable
    {
        private MQTTPG PGService { get => Device.DService; }
        private DevicePG Device { get; set; }
        public string DisPlayName => Device.Config.Name;


        public DisplayPG(DevicePG devicePG)
        {
            Device = devicePG;
            InitializeComponent();
            DataContext = Device;
        }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            PGService_DeviceStatusChanged(sender,PGService.DeviceStatus);
            PGService.DeviceStatusChanged += PGService_DeviceStatusChanged;
            if (PGService.Config.IsNet)
            {
                TextBlockPGIP.Text = Properties.Resources.IPAddress;
                TextBlockPGPort.Text = Properties.Resources.Port;
            }
            else
            {
                TextBlockPGIP.Text = Properties.Resources.Serial;
                TextBlockPGPort.Text = Properties.Resources.BaudRate;
            }
            this.ApplyChangedSelectedColor(DisPlayBorder);
        }

        private void PGService_DeviceStatusChanged(object? sender, DeviceStatusType e)
        {
            void SetVisibility(UIElement element, Visibility visibility) { if (element.Visibility != visibility) element.Visibility = visibility; }

            void HideAllButtons()
            {
                SetVisibility(ButtonUnauthorized, Visibility.Collapsed);
                SetVisibility(TextBlockUnknow, Visibility.Collapsed);
                SetVisibility(StackPanelContent, Visibility.Collapsed);
                SetVisibility(TextBlockOffLine, Visibility.Collapsed);
            }

            HideAllButtons();
            switch (e)
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
                    break;
                case DeviceStatusType.Closed:
                    SetVisibility(StackPanelContent, Visibility.Visible);
                    break;
                case DeviceStatusType.Opened:
                    SetVisibility(StackPanelContent, Visibility.Visible);
                    break;
                default:
                    SetVisibility(StackPanelContent, Visibility.Visible);
                    break;
            }
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }


        private void DoOpen(Button button)
        {
            string btnTitle = button.Content.ToString();
            if (btnTitle != null && btnTitle.Equals(Properties.Resources.Open, StringComparison.Ordinal))
            {
                button.Content = Properties.Resources.Opening;
                int port;
                if (!int.TryParse(TextBoxPGPort.Text, out port))
                {
                    MessageBox1.Show(Application.Current.MainWindow, Properties.Resources.PortConfigError);
                    return;
                }
                if (PGService.Config.IsNet) PGService.Open(CommunicateType.Tcp, TextBoxPGIP.Text, port);
                else PGService.Open(CommunicateType.Serial, TextBoxPGIP.Text, port);
            }
            else
            {
                button.Content = Properties.Resources.Closing;
                PGService.Close();
            }
        }

        private void PGOpen(object sender, RoutedEventArgs e)
        {
            if (sender is Button button) DoOpen(button);
        }

        private void PGStartPG(object sender, RoutedEventArgs e) => PGService.PGStartPG();

        private void PGStopPG(object sender, RoutedEventArgs e) => PGService.PGStopPG();

        private void PGReSetPG(object sender, RoutedEventArgs e) => PGService.PGReSetPG();
        private void PGSwitchUpPG(object sender, RoutedEventArgs e) => PGService.PGSwitchUpPG();
        private void PGSwitchDownPG(object sender, RoutedEventArgs e) => PGService.PGSwitchDownPG();

        private void PGSwitchFramePG(object sender, RoutedEventArgs e) => PGService.PGSwitchFramePG(int.Parse(PGFrameText.Text));

        private void PGSendCmd(object sender, RoutedEventArgs e)
        {
            PGService.CustomPG(PGCmdMsg.Text);
        }

        public void Dispose()
        {
            PGService.DeviceStatusChanged -= PGService_DeviceStatusChanged;
        }
    }
}
