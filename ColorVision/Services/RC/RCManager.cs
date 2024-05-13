using ColorVision.Common.Utilities;
using ColorVision.Services.RC;
using ColorVision.Settings;
using ColorVision.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Services.RC
{
    public class RCManager
    {
        public  log4net.ILog Log { get; set; } = log4net.LogManager.GetLogger(typeof(RCManager));
        private static readonly object _locker = new();
        private static RCManager _instance;
        public static RCManager GetInstance() { lock (_locker) { return _instance ??= new RCManager(); } }

        private RCManager()
        {
            ServiceController = new ServiceController("RegistrationCenterService");

        }
        private ServiceController ServiceController { get; set; }

        public bool IsLocalServiceRunning() 
        {
            try
            {
                return ServiceController != null && ServiceController.Status == ServiceControllerStatus.Running;

            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }
        }


        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public void OpenCVWinSMS()
        {
            Log.Info("RegistrationCenterService opend");
            Process[] processes = Process.GetProcessesByName("CVWinSMS");
            if (processes.Length > 0)
            {
                // 如果程序已经在运行，则激活该程序的窗口
                SetForegroundWindow(processes[0].MainWindowHandle);
            }
            else
            {
                string? RegistrationCenterServicePath = Tool.GetServicePath("RegistrationCenterService");
                if (RegistrationCenterServicePath != null)
                {
                    string Dir = Path.GetDirectoryName(RegistrationCenterServicePath);
                    Dir = Path.GetDirectoryName(Dir);
                    string FilePath = Dir + "\\InstallTool\\CVWinSMS.exe";
                    PlatformHelper.Open(FilePath);
                }
            }
        }

        public static async Task CheckLocalService()
        {
            await Task.Delay(2000);
            try
            {
                string excmd = string.Empty;
                ServiceController sc = new("RegistrationCenterService");
                if (sc.Status == ServiceControllerStatus.Stopped)
                {
                    excmd += "net start RegistrationCenterService";
                }
                if (!string.IsNullOrEmpty(excmd))
                {
                    excmd += "1";
                    Tool.ExecuteCommandAsAdmin(excmd);
                }
            }
            catch 
            { 
            }
        }
    }
}
