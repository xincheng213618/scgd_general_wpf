using System;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Services.Terminal
{
    /// <summary>
    /// TerminalServiceControl.xaml 的交互逻辑
    /// </summary>
    public partial class TerminalServiceControl : UserControl
    {
        public TerminalService ServiceTerminal { get; set; }  

        public TerminalServiceControl(TerminalService mQTTService)
        {
            ServiceTerminal = mQTTService;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = ServiceTerminal;
        }

        private void ListViewService_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView { SelectedItem: DeviceService device })
                device.IsSelected = true;
        }

    }
}
