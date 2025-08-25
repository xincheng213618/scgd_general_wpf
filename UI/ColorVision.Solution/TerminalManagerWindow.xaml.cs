using ColorVision.UI.Menus;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

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
    /// </summary>
    public partial class TerminalManagerWindow : Window
    {
        public TerminalManagerWindow()
        {
            InitializeComponent();
        }
        private Process _process;

        private void Window_Initialized(object sender, EventArgs e)
        {
            StartTerminal();
        }

        private void StartTerminal()
        {
            _process = new Process();
            _process.StartInfo.FileName = "cmd.exe";
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.RedirectStandardInput = true;
            _process.StartInfo.RedirectStandardOutput = true;
            _process.StartInfo.RedirectStandardError = true;

            _process.StartInfo.CreateNoWindow = true;

            _process.OutputDataReceived += (s, e) => AppendText(e.Data);
            _process.ErrorDataReceived += (s, e) => AppendText(e.Data);

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        private void AppendText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            Dispatcher.Invoke(() =>
            {
                rtbOutput.AppendText(text + "\n");
                rtbOutput.ScrollToEnd();
            });
        }

        private void tbInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string command = tbInput.Text;
                if (_process != null && !_process.HasExited)
                {
                    _process.StandardInput.WriteLine(command);
                }
                tbInput.Clear();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
            }
        }

    }
}
