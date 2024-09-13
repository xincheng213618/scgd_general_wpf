#pragma warning disable SYSLIB0014
using ColorVision.UI.Menus;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows;

namespace WindowsServicePlugin
{
    public class InstallMQTT : MenuItemBase
    {
        public InstallMQTT()
        {
        }

        public override string OwnerGuid => "ServiceLog";

        public override string GuidId => "InstallMQTT";

        public override int Order => 99;

        public override string Header => "安装MQTT";

        private string url = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/MQTT/mosquitto-2.0.18-install-windows-x64.exe";
        private string downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" +  @"ColorVision\\mosquitto-2.0.18-install-windows-x64.exe";

        public override void Execute()
        {
            Task.Run(() =>
            {
                if (!File.Exists(downloadPath))
                {
                    try
                    {
                        DownloadFile(url, downloadPath, "1", "1");
                    }
                    catch
                    {
                        DownloadFile(url, downloadPath);
                    }
                }
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
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                    File.Delete(downloadPath);
                }
            });

        }


        static void DownloadFile(string url, string destinationPath, string? username =null, string? password = null)
        {
            using WebClient client = new WebClient();

            if (!string.IsNullOrWhiteSpace(username))

                // 添加凭证
                client.Credentials = new NetworkCredential(username, password);

            // 下载文件
            client.DownloadFile(url, destinationPath);
        }
    }



}
