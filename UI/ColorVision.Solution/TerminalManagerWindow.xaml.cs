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
        public override string Header => ColorVision.Solution.Properties.Resources.Terminal;

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
        private const double CharWidth = 8.0;  // Approximate character width for Consolas 12pt
        private const double CharHeight = 16.0; // Approximate character height for Consolas 12pt

        public TerminalManagerWindow()
        {
            InitializeComponent();
            this.SizeChanged += Window_SizeChanged;
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            StartTerminal();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_terminal != null && rtbOutput != null)
            {
                // Calculate terminal size based on RichTextBox dimensions
                var cols = (short)Math.Max(20, (rtbOutput.ActualWidth - 20) / CharWidth);
                var rows = (short)Math.Max(10, (rtbOutput.ActualHeight - 20) / CharHeight);
                
                try
                {
                    _terminal.Resize(cols, rows);
                }
                catch (Exception ex)
                {
                    // Resize might fail if terminal is not ready, ignore
                    System.Diagnostics.Debug.WriteLine($"Resize failed: {ex.Message}");
                }
            }
        }

        private void StartTerminal()
        {
            try
            {
                _terminal = new ConPtyTerminal();
                _terminal.OutputReceived += OnTerminalOutput;

                // Calculate terminal size based on current window size
                var cols = (short)Math.Max(20, (rtbOutput.ActualWidth - 20) / CharWidth);
                var rows = (short)Math.Max(10, (rtbOutput.ActualHeight - 20) / CharHeight);

                // Use default if window not yet laid out
                if (cols < 20 || rows < 10)
                {
                    cols = 80;
                    rows = 25;
                }

                _terminal.Start(cols, rows, "cmd.exe");
            }
            catch (Exception ex)
            {
                // Display error in red using ANSI color code
                AppendText($"\x1b[31mFailed to start terminal: {ex.Message}\x1b[0m\n");
            }
        }

        private void OnTerminalOutput(object? sender, string output)
        {
            Dispatcher.Invoke(() =>
            {
                AppendText(output);
            });
        }

        private void AppendText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            // Parse ANSI escape sequences and create formatted text
            var defaultForeground = Colors.White;
            var defaultBackground = Colors.Black;
            var inlines = AnsiEscapeSequenceParser.Parse(text, defaultForeground, defaultBackground);

            var paragraph = new Paragraph
            {
                Margin = new Thickness(0)
            };

            foreach (var inline in inlines)
            {
                paragraph.Inlines.Add(inline);
            }

            rtbOutput.Document.Blocks.Add(paragraph);
            rtbOutput.ScrollToEnd();
        }

        private void tbInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (_terminal == null) return;

            string? inputToSend = null;

            // Handle special keys
            switch (e.Key)
            {
                case Key.Enter:
                    inputToSend = tbInput.Text + "\r\n";
                    tbInput.Clear();
                    break;

                case Key.Up:
                    inputToSend = "\x1b[A"; // VT100 escape sequence for up arrow
                    break;

                case Key.Down:
                    inputToSend = "\x1b[B"; // VT100 escape sequence for down arrow
                    break;

                case Key.Right:
                    inputToSend = "\x1b[C"; // VT100 escape sequence for right arrow
                    break;

                case Key.Left:
                    inputToSend = "\x1b[D"; // VT100 escape sequence for left arrow
                    break;

                case Key.Home:
                    inputToSend = "\x1b[H"; // VT100 escape sequence for home
                    break;

                case Key.End:
                    inputToSend = "\x1b[F"; // VT100 escape sequence for end
                    break;

                case Key.Delete:
                    inputToSend = "\x1b[3~"; // VT100 escape sequence for delete
                    break;

                case Key.Tab:
                    inputToSend = "\t";
                    break;

                case Key.Back:
                    inputToSend = "\b";
                    break;

                case Key.Escape:
                    inputToSend = "\x1b";
                    break;

                case Key.C:
                    // Handle Ctrl+C
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        inputToSend = "\x03"; // ETX (End of Text) - Ctrl+C
                    }
                    break;

                case Key.D:
                    // Handle Ctrl+D (EOF)
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        inputToSend = "\x04"; // EOT (End of Transmission) - Ctrl+D
                    }
                    break;

                case Key.Z:
                    // Handle Ctrl+Z (suspend)
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        inputToSend = "\x1a"; // SUB - Ctrl+Z
                    }
                    break;

                case Key.A:
                    // Handle Ctrl+A (beginning of line)
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        inputToSend = "\x01"; // SOH - Ctrl+A
                    }
                    break;

                case Key.E:
                    // Handle Ctrl+E (end of line)
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        inputToSend = "\x05"; // ENQ - Ctrl+E
                    }
                    break;

                case Key.K:
                    // Handle Ctrl+K (kill line)
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        inputToSend = "\x0b"; // VT - Ctrl+K
                    }
                    break;

                case Key.U:
                    // Handle Ctrl+U (clear line)
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        inputToSend = "\x15"; // NAK - Ctrl+U
                    }
                    break;

                case Key.L:
                    // Handle Ctrl+L (clear screen)
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        inputToSend = "\x0c"; // FF - Ctrl+L
                    }
                    break;

                default:
                    // Let the textbox handle other keys normally
                    return;
            }

            if (inputToSend != null)
            {
                _terminal.SendInput(inputToSend);
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
