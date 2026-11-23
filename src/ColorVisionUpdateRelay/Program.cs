using System;
using System.Collections.Generic;
using System.Net;
using System.Windows.Forms;

namespace ColorVisionUpdateRelay
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // Re-resolve the update host on every check so a hosts change is seen without restarting the tool.
            ServicePointManager.DnsRefreshTimeout = 0;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 0)
            {
                Application.Run(new MainForm());
                return 0;
            }
            if (args.Length != 2 || args[0] != "--relay-action" || (args[1] != "enable" && args[1] != "disable"))
                return 2;
            return ConfigureRelay(args[1] == "enable");
        }

        private static int ConfigureRelay(bool enable)
        {
            var output = new List<string>();
            try
            {
                if (enable)
                {
                    string loop = RelayOperations.CheckRelayLoop(RelayOperations.DefaultServerHost);
                    if (loop != null) throw new InvalidOperationException(loop);
                    RelayOperations.EnableForward(RelayOperations.DefaultServerHost, RelayOperations.DefaultPort, output.Add);
                }
                else
                {
                    RelayOperations.DisableForward(RelayOperations.DefaultServerHost, RelayOperations.DefaultPort, output.Add);
                }
                return 0;
            }
            catch (Exception ex)
            {
                string details = string.Join(Environment.NewLine, output);
                string message = ex.Message;
                if (details.Length > 0) message += Environment.NewLine + Environment.NewLine + details;
                MessageBox.Show(message, enable ? "开启更新转发失败" : "移除更新转发失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 1;
            }
        }
    }
}
