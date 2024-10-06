#pragma warning disable SYSLIB0014
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Services.RC;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using Mysqlx.Prepare;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Xml.Linq;

namespace WindowsServicePlugin
{
    public class CVWinSMSConfig : IConfig
    {
        public static CVWinSMSConfig Instance => ConfigService.Instance.GetRequiredService<CVWinSMSConfig>();

        public string CVWinSMSPath { get => _CVWinSMSPath; set => _CVWinSMSPath = value; }
        private string _CVWinSMSPath = string.Empty;

        public string Version { get => _Version; set => _Version = value; }
        private string _Version = string.Empty;
    }


    public class SetMysqlConfig : IWizardStep
    {
        public int Order => 55;

        public string Header => "从服务中配置Mysql";

        public virtual RelayCommand Command => new(A => Execute(), b => AccessControl.Check(Execute));

        Dictionary<string, string> dic = new Dictionary<string, string>();  
        public void Execute()
        {
            if (!File.Exists(CVWinSMSConfig.Instance.CVWinSMSPath))
            {
                MessageBox.Show("请先配置服务管理工具");
                return;
            }
            string filePath = Directory.GetParent(CVWinSMSConfig.Instance.CVWinSMSPath) + @"\config\App.config";

            if (!File.Exists(filePath))
            {
                MessageBox.Show("请先运行服务管理工具");
                return;
            }
            // Load the XML document
            XDocument config = XDocument.Load(filePath);

            // Query the appSettings
            var appSettings = config.Element("configuration")?.Element("appSettings")?.Elements("add");

            if (appSettings != null)
            {
                foreach (var setting in appSettings)
                {
                    string key = setting.Attribute("key")?.Value;
                    string value = setting.Attribute("value")?.Value;
                    if (key != null && value !=null)
                    {
                        dic.Add(key, value);
                    }
                }
                MySqlSetting.Instance.MySqlConfig.UserName = dic["MysqlUser"];
                MySqlSetting.Instance.MySqlConfig.UserPwd = dic["MysqlPwd"];
                MySqlSetting.Instance.MySqlConfig.Database = dic["MysqlDatabase"];
                MySqlConfig mySqlConfig = new MySqlConfig() { Host = dic["MysqlHost"], UserName = "root", UserPwd = dic["MysqlRootPwd"], Database = dic["MysqlDatabase"] };

                CVWinSMSConfig.Instance.Version = dic["Version"];
                RCSetting.Instance.Config.RCName = dic["RCName"];
                MessageBox.Show("配置成功");
            }
            else
            {
            }
        }
    }


    public class InstallTool : MenuItemBase, IWizardStep
    {

        public override string OwnerGuid => "ServiceLog";

        public override string GuidId => "InstallTool";

        public override int Order => 99;

        public override string Header => Properties.Resources.ManagementService;

        public DownloadFile DownloadFile { get; set; } = new DownloadFile();
        public InstallTool()
        {
            DownloadFile = new DownloadFile();
            DownloadFile.DownloadTile = "下载服务管理工具";
        }

        private string url = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/InstallTool/InstallTool[2.0.0.24092].zip";
        private string downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + @"ColorVision\\InstallTool[2.0.0.24092].zip";


        public void Download()
        {
            WindowUpdate windowUpdate = new WindowUpdate(DownloadFile);
            if (!File.Exists(downloadPath))
            {
                windowUpdate.Show();
            }

            Task.Run(async () =>
            {
                if (!File.Exists(downloadPath))
                {
                    await DownloadFile.GetIsPassWorld();
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

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Process.GetProcessesByName("CVWinSMS").ToList().ForEach(p => p.Kill());
                    using (System.Windows.Forms.FolderBrowserDialog folderBrowser = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        folderBrowser.Description = "请选择解压缩目录";
                        folderBrowser.ShowNewFolderButton = true;
                        folderBrowser.RootFolder = Environment.SpecialFolder.Desktop;
                        if (folderBrowser.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                        ZipFile.ExtractToDirectory(downloadPath, folderBrowser.SelectedPath, true);

                        CVWinSMSConfig.Instance.CVWinSMSPath = folderBrowser.SelectedPath + "\\InstallTool\\CVWinSMS.exe";
                    }


                    // 启动新的实例
                    ProcessStartInfo startInfo = new();
                    startInfo.UseShellExecute = true; // 必须为true才能使用Verb属性
                    startInfo.WorkingDirectory = Environment.CurrentDirectory;
                    startInfo.FileName = CVWinSMSConfig.Instance.CVWinSMSPath;
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

            });
        }

        public override void Execute()
        {
            if (!File.Exists(CVWinSMSConfig.Instance.CVWinSMSPath))
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), "找不到管理工具，是否下载", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    Download();
                    return;
                }


                if (MessageBox.Show(Application.Current.GetActiveWindow(), "I can't find CVWinSMS (CVWinSMS.exe). Would you like to help me find it?", "Open in CVWinSMS", MessageBoxButton.YesNo) == MessageBoxResult.Yes) return;
                using (System.Windows.Forms.OpenFileDialog openFileDialog = new())
                {
                    openFileDialog.Title = "Select CVWinSMS.exe";
                    openFileDialog.Filter = "CVWinSMS.exe|CVWinSMS.exe";
                    openFileDialog.RestoreDirectory = true;

                    if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    CVWinSMSConfig.Instance.CVWinSMSPath = openFileDialog.FileName;
                }
            }
            try
            {
                Process.Start(CVWinSMSConfig.Instance.CVWinSMSPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
            }
        }

    }



}
