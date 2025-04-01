#pragma warning disable SYSLIB0014
using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Menus;
using iText.Layout.Element;
using log4net;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Xml.Linq;


namespace WindowsServicePlugin
{
    public class CVWinSMSConfig : ViewModelBase, IConfig,IConfigSettingProvider   
    {
        public static CVWinSMSConfig Instance => ConfigService.Instance.GetRequiredService<CVWinSMSConfig>();

        public string CVWinSMSPath { get => _CVWinSMSPath; set  { _CVWinSMSPath = value; } }
        private string _CVWinSMSPath = string.Empty;

        [JsonIgnore]
        public string BaseLocation { 
            
            get
            {
                if (dic.TryGetValue("BaseLocation", out string location))
                    return location;
                return string.Empty;
            }
        }

        [JsonIgnore]
        Dictionary<string, string> dic = new Dictionary<string, string>();

        public void Init()
        {
            try
            {
                string filePath = Directory.GetParent(CVWinSMSPath) + @"\config\App.config";
                if (!File.Exists(filePath))
                {
                    return;
                }
                XDocument config = XDocument.Load(filePath);
                var appSettings = config.Element("configuration")?.Element("appSettings")?.Elements("add");

                if (appSettings != null)
                {
                    foreach (var setting in appSettings)
                    {
                        string key = setting.Attribute("key")?.Value;
                        string value = setting.Attribute("value")?.Value;
                        if (key != null && value != null)
                        {
                            if (!dic.TryAdd(key, value))
                            {
                                dic[key] = value;
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                return;
            }


        }

        public string Version { get => _Version; set => _Version = value; }
        private string _Version = string.Empty;

        public string UpdatePath { get => _UpdatePath; set { _UpdatePath = value; NotifyPropertyChanged(); } }
        private string _UpdatePath = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/InstallTool";

        public bool IsAutoUpdate { get => _IsAutoUpdate; set { _IsAutoUpdate = value; NotifyPropertyChanged(); } }
        private bool _IsAutoUpdate = true;

        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>()
            {
                new ConfigSettingMetadata()
                {
                    Name = "CVWinSMSPath",
                    Description = "CVWinSMSPath",
                    Type = ConfigSettingType.Text,
                    BindingName = nameof(CVWinSMSPath),
                    Source =Instance
                },
                new ConfigSettingMetadata
                {
                    Name = "CVWinSMSIsAutoUpdate",
                    Description =  "",
                    Order = 999,
                    Type = ConfigSettingType.Bool,
                    BindingName =nameof(IsAutoUpdate),
                    Source = Instance,
                }
            };

        }
    }





    public class InstallTool : MenuItemBase, IWizardStep, IMainWindowInitialized
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(InstallTool));


        public override string OwnerGuid => "ServiceLog";

        public override string GuidId => "InstallTool";

        public override int Order => 1;

        public override string Header => Properties.Resources.ManagementService;

        public string Description => GetDescription();

        public string GetDescription()
        {
            string Description = "打开最新的服务管理工具，如果不存在会自动下载，下载后请手动指定保存位置";
            if (File.Exists(CVWinSMSConfig.Instance.CVWinSMSPath))
            {
                string filePath = Directory.GetParent(CVWinSMSConfig.Instance.CVWinSMSPath) + @"\config\App.config";
                if (File.Exists(filePath))
                {
                    Description += Environment.NewLine + "配置文件路径：" + filePath;
                    Description += Environment.NewLine + File.ReadAllText(filePath);
                }
            }
            return Description;
        }

        public DownloadFile DownloadFile { get; set; } = new DownloadFile();

        public static CVWinSMSConfig Config => CVWinSMSConfig.Instance;
        public static string LatestReleaseUrl => Config.UpdatePath + "/LATEST_RELEASE";


        private string url = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/InstallTool/InstallTool[2.1.0.24111].zip";
        private string downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + @"ColorVision\\";


        public InstallTool()
        {
            DownloadFile = new DownloadFile();
            DownloadFile.DownloadTile = "下载服务管理工具";
        }
        public async Task Initialize()
        {
            // 如果是调试模式，不进行更新检测
            if (Debugger.IsAttached) return;

            if (Config.IsAutoUpdate)
            {
                await GetLatestReleaseVersion();
            }
        }
        public bool ConfigurationStatus { get => _ConfigurationStatus; set { _ConfigurationStatus = value; NotifyPropertyChanged(); } }
        private bool _ConfigurationStatus = File.Exists(CVWinSMSConfig.Instance.CVWinSMSPath);

        public async Task GetLatestReleaseVersion()
        {
            try
            {
                if (!File.Exists(Config.CVWinSMSPath))
                    return;
                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(Config.CVWinSMSPath);
                Version CurrentVerision = new Version(versionInfo.FileVersion);

                Version version = await DownloadFile.GetLatestVersionNumber(LatestReleaseUrl);
                if (version > CurrentVerision)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (MessageBox.Show(Application.Current.GetActiveWindow(), "服务管理工具:找到新版本，是否更新", "CVWinSMS", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + $"ColorVision\\InstallTool[{version}].zip";
                            url = $"http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/InstallTool/InstallTool[{version}].zip";
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

                                _= Task.Run(async () =>
                                {
                                    Process.GetProcessesByName("CVWinSMS").ToList().ForEach(p => p.Kill());
                                    log.Info("正在关闭CVWinSMS");
                                    await Task.Delay(3000);
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        try
                                        {
                                            string? folderBrowser = Directory.GetParent(Directory.GetParent(CVWinSMSConfig.Instance.CVWinSMSPath)?.FullName)?.FullName;
                                            if (folderBrowser != null)
                                            {
                                                ZipFile.ExtractToDirectory(downloadPath, folderBrowser, true);
                                            }
                                            else
                                            {
                                                MessageBox.Show("更新失败， 找不到更新所在的文件夹");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            MessageBox.Show("更新失败，" + ex.Message);

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

                            });

                        };

                    });

                }
            }catch(Exception ex)
            {
                log.Error(ex);
            }
        }

        public async void Download()
        {
            Version version = await DownloadFile.GetLatestVersionNumber(LatestReleaseUrl);
            downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + $"ColorVision\\InstallTool[{version}].zip";
            url = $"http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/InstallTool/InstallTool[{version}].zip";

            Application.Current.Dispatcher.Invoke(() =>
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

                        ConfigurationStatus = File.Exists(CVWinSMSConfig.Instance.CVWinSMSPath);

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
            });
        }

        public override void Execute()
        {
            if (!File.Exists(CVWinSMSConfig.Instance.CVWinSMSPath))
            {
                Process[] processes = Process.GetProcessesByName("CVWinSMS");
                if (processes.Length == 1)
                {
                    foreach (Process process in processes)
                    {
                        try
                        {
                            // 获取进程的主模块文件路径
                            CVWinSMSConfig.Instance.CVWinSMSPath = process.MainModule.FileName;
                            //string filePath = process.MainModule.FileName;
                            log.Info($"进程ID: {process.Id}, 文件路径: {CVWinSMSConfig.Instance.CVWinSMSPath}");
                            return;
                        }
                        catch (Exception ex)
                        {
                            log.Debug($"无法获取进程的文件路径: {ex.Message}");
                        }
                    }
                }

                if (MessageBox.Show(Application.Current.GetActiveWindow(), "找不到管理工具，是否下载", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    Task.Run(() => Download());
                    return;
                }

                if (MessageBox.Show(Application.Current.GetActiveWindow(), "I can't find CVWinSMS (CVWinSMS.exe). Would you like to help me find it?", "Open in CVWinSMS", MessageBoxButton.YesNo) == MessageBoxResult.No) return;
                using (System.Windows.Forms.OpenFileDialog openFileDialog = new())
                {
                    openFileDialog.Title = "Select CVWinSMS.exe";
                    openFileDialog.Filter = "CVWinSMS.exe|CVWinSMS.exe";
                    openFileDialog.RestoreDirectory = true;

                    if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    CVWinSMSConfig.Instance.CVWinSMSPath = openFileDialog.FileName;
                }
            }
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
                ConfigurationStatus = File.Exists(CVWinSMSConfig.Instance.CVWinSMSPath);
                Process p = Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
            }
        }


    }
}
