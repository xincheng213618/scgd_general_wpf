using ColorVision.UI.Menus;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Media;

namespace ColorVision.Solution
{

    public class MenuConfigManagerWindow : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override string Header => "终端";

        public override int Order => 9009;

        public override void Execute()
        {
            new TerminalManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }


    /// <summary>
    /// TerminalManagerWindow.xaml 的交互逻辑
    /// ConPTY-based terminal with full ANSI/VT100 support
    /// </summary>
    public partial class TerminalManagerWindow : Window
    {
        private ConPtyTerminal? _terminal;

        public TerminalManagerWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            StartTerminal();
        }

        private void StartTerminal()
        {
            try
            {
                _terminal = new ConPtyTerminal();
                _terminal.OutputReceived += OnTerminalOutput;

                // Calculate terminal size based on window size
                // Using approximate character dimensions (will be refined)
                short cols = 80;
                short rows = 25;

                _terminal.Start(cols, rows, "cmd.exe");
            }
            catch (Exception ex)
            {
                AppendText($"Failed to start terminal: {ex.Message}\n", Brushes.Red);
            }
        }

        private void OnTerminalOutput(object? sender, string output)
        {
            Dispatcher.Invoke(() =>
            {
                AppendText(output, Brushes.White);
            });
        }

        private void AppendText(string text, Brush? foreground = null)
        {
            if (string.IsNullOrEmpty(text)) return;

            var paragraph = new Paragraph(new Run(text))
            {
                Margin = new Thickness(0),
                Foreground = foreground ?? Brushes.White
            };

            rtbOutput.Document.Blocks.Add(paragraph);
            rtbOutput.ScrollToEnd();
        }

        private void tbInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string input = tbInput.Text;
                if (_terminal != null)
                {
                    _terminal.SendInput(input + "\r\n");
                }
                tbInput.Clear();
                e.Handled = true;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _terminal?.Dispose();
        }
    }
}
