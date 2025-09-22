using ColorVision.Common.Utilities;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace WindowsServicePlugin
{
    public class InstallMQTT :  WizardStepBase
    {
        public override int Order => 99;
        public override string Header => "安装MQTT";
        public override string Description => "安装本地的MQTT服务，如果使用其他机器作为转发请跳过";  

        public DownloadFile DownloadFile { get; set; } = new DownloadFile();
        public InstallMQTT()
        {
            DownloadFile = new DownloadFile();
            DownloadFile.DownloadTile = "下载MQTT";
        }

        private string url = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/MQTT/mosquitto-2.0.18-install-windows-x64.exe";
        private string downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" +  @"ColorVision\\mosquitto-2.0.18-install-windows-x64.exe";

        public override void Execute()
        {
            WindowUpdate windowUpdate = new WindowUpdate(DownloadFile) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            if (!File.Exists(downloadPath))
            {
                windowUpdate.Show();
            }

            Task.Run(async () =>
            {
                if (!File.Exists(downloadPath))
                {
                    CancellationTokenSource _cancellationTokenSource = new();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        windowUpdate.Show();
                    });
                    await DownloadFile.Download(url, downloadPath, _cancellationTokenSource.Token);
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    windowUpdate.Close();
                });
                // 启动新的实例
                ProcessStartInfo startInfo = new();
                startInfo.UseShellExecute = true; // 必须为true才能使用Verb属性
                startInfo.WorkingDirectory = Environment.CurrentDirectory;
                startInfo.FileName = downloadPath;
                startInfo.Verb = "runas"; // "runas"指定启动程序时请求管理员权限
                                          // 如果需要静默安装，添加静默安装参数
                                          //quiet 没法自启，桌面图标也是空                       
                                          //startInfo.Arguments = "/quiet";

                try
                {
                    Process p = Process.Start(startInfo);
                    p?.WaitForExit();
                    Tool.ExecuteCommandAsAdmin("net start mosquitto");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                    File.Delete(downloadPath);
                }
            });


        }

    }



}
