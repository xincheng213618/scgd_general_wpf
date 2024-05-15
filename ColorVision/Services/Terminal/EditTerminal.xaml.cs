using ColorVision.Common.MVVM;
using System;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Services.Terminal
{
    /// <summary>
    /// EditTerminal.xaml 的交互逻辑
    /// </summary>
    public partial class EditTerminal : Window
    {
        public TerminalService TerminalService { get; set; }
        public TerminalServiceConfig EditConfig { get; set; }

        public EditTerminal(TerminalService terminalService)
        {
            TerminalService = terminalService;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = TerminalService;
            EditConfig = TerminalService.Config.Clone();
            EditContent.DataContext = EditConfig;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            TerminalService.Config.CopyFrom(EditConfig);
            Close();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }
    }
}
