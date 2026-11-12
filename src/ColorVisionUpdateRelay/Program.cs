using System;
using System.Net;
using System.Windows.Forms;

namespace ColorVisionUpdateRelay
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // Re-resolve the update host on every check so a hosts change is seen without restarting the tool.
            ServicePointManager.DnsRefreshTimeout = 0;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
