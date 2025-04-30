#pragma warning disable CS8604
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using log4net;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Plugins
{
    public class PluginWindowConfigProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>
            {
                 new ConfigSettingMetadata
                {
                    Name ="主动检测插件更新",
                    Description =  "主动检测插件更新",
                    Order = 999,
                    Type = ConfigSettingType.Bool,
                    BindingName =nameof(PluginWindowConfig.IsAutoUpdate),
                    Source = PluginWindowConfig.Instance,
                }
            };
        }
    }


    public class PluginWindowConfig : WindowConfig
    {
        public static PluginWindowConfig Instance => ConfigService.Instance.GetRequiredService<PluginWindowConfig>();

        /// <summary>
        /// 是否自动更新插件
        /// </summary>
        public bool IsAutoUpdate { get => _IsAutoUpdate; set { _IsAutoUpdate = value; NotifyPropertyChanged(); } }
        private bool _IsAutoUpdate = true;
    }


    public class PluginManager:ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PluginManager));
        private static PluginManager _instance;
        private static readonly object _locker = new();
        public static PluginManager GetInstance() { lock (_locker) { _instance ??= new PluginManager(); return _instance; } }
        public ObservableCollection<PluginInfo> Plugins { get; private set; } = new ObservableCollection<PluginInfo>();
        public static PluginWindowConfig Config => PluginWindowConfig.Instance;
        public RelayCommand EditConfigCommand { get; set; }

        public RelayCommand OpenStoreCommand { get;  set; }
        public RelayCommand InstallPackageCommand { get; set; }
        public RelayCommand DownloadPackageCommand { get; set; }

        private DownloadFile DownloadFile { get; set; }
        public PluginManager()
        {
            log.Info("正在检索是否存在附加项目");
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IPlugin plugin)
                    {
                        PluginInfo info = new PluginInfo(plugin, assembly);
                        info.AssemblyVersion = assembly.GetName().Version;
                        info.AssemblyBuildDate = File.GetLastWriteTime(assembly.Location);

                        Plugins.Add(info);
                        log.Info($"找到外加插件：{plugin} 名称：{info.AssemblyName} 版本：{info.AssemblyVersion} " +
                                 $"日期：{info.AssemblyBuildDate} 路径：{info.AssemblyPath} 文化：{info.AssemblyCulture} " +
                                 $"公钥标记：{info.AssemblyPublicKeyToken}");
                    }
                }
            }
            OpenStoreCommand = new RelayCommand(a => OpenStore());
            InstallPackageCommand = new RelayCommand(a => InstallPackage());
            DownloadPackageCommand = new RelayCommand(a => DownloadPackage());
            DownloadFile = new DownloadFile();
            EditConfigCommand = new RelayCommand(a => new PropertyEditorWindow(Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
        }

        public string SearchName { get => _SearchName; set { _SearchName = value; NotifyPropertyChanged(); }}
        private string _SearchName;
        public async void DownloadPackage()
        {
            string LatestReleaseUrl = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Plugins/" + SearchName + "/LATEST_RELEASE";
            DownloadFile.DownloadTile = "下载" + SearchName;
            Version version = await DownloadFile.GetLatestVersionNumber(LatestReleaseUrl);
            if (version == new Version())
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"找到不到项目{SearchName}");
                return;
            }
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), $"找到项目{SearchName}，版本{version}，是否下载", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    string downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + $"ColorVision\\{SearchName}-{version}.zip";
                    string url = $"http://xc213618.ddns.me:9999/D%3A/ColorVision/Plugins/{SearchName}/{SearchName}-{version}.zip";
                    WindowUpdate windowUpdate = new WindowUpdate(DownloadFile) { Owner =Application.Current.GetActiveWindow(), WindowStartupLocation =WindowStartupLocation.CenterOwner };
                    if (File.Exists(downloadPath))
                    {
                        File.Delete(downloadPath);
                    }
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

                            try
                            {
                                // 解压缩 ZIP 文件到临时目录
                                string tempDirectory = Path.Combine(Path.GetTempPath(), "ColorVisionPluginsUpdate");
                                if (Directory.Exists(tempDirectory))
                                {
                                    Directory.Delete(tempDirectory, true);
                                }
                                ZipFile.ExtractToDirectory(downloadPath, tempDirectory);

                                // 创建批处理文件内容
                                string batchFilePath = Path.Combine(tempDirectory, "update.bat");
                                string programPluginsDirectory = AppDomain.CurrentDomain.BaseDirectory + "Plugins";

                                string targetPluginDirectory = Path.Combine(programPluginsDirectory, SearchName);

                                string? executableName = Path.GetFileName(Environment.ProcessPath);

                                string batchContent = $@"
@echo off
taskkill /f /im ""{executableName}""
timeout /t 1
xcopy /y /e ""{tempDirectory}\*"" ""{programPluginsDirectory}""
start """" ""{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, executableName)}""  -c MenuPluginManager
rd /s /q ""{tempDirectory}""
del ""%~f0"" & exit
";
                                File.WriteAllText(batchFilePath, batchContent);

                                // 设置批处理文件的启动信息
                                ProcessStartInfo startInfo = new()
                                {
                                    FileName = batchFilePath,
                                    UseShellExecute = true,
                                    Verb = "runas" // 请求管理员权限
                                };
                                // 启动批处理文件并退出当前程序
                                Process.Start(startInfo);
                                Environment.Exit(0);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"更新失败: {ex.Message}");
                            }
                        });

                    });

                };

            });



        }


        public static void InstallPackage()
        {
            // 打开文件选择对话框
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "ZIP Files (*.zip)|*.zip",
                Title = "Select a ZIP file"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedZipPath = openFileDialog.FileName;
                InstallFromZip(selectedZipPath);
            }
        }
        private static void InstallFromZip(string zipFilePath)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // 解压缩 ZIP 文件到临时目录
                    string tempDirectory = Path.Combine(Path.GetTempPath(), "ColorVisionPluginsUpdate");
                    if (Directory.Exists(tempDirectory))
                    {
                        Directory.Delete(tempDirectory, true);
                    }
                    ZipFile.ExtractToDirectory(zipFilePath, tempDirectory);

                    // 创建批处理文件内容
                    string batchFilePath = Path.Combine(tempDirectory, "update.bat");
                    string programPluginsDirectory = AppDomain.CurrentDomain.BaseDirectory + "/Plugins";

                    string? executableName = Path.GetFileName(Environment.ProcessPath);

                    string batchContent = $@"
@echo off
taskkill /f /im ""{executableName}""
timeout /t 1
xcopy /y /e ""{tempDirectory}\*"" ""{programPluginsDirectory}""
start """" ""{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, executableName)}""  -c MenuPluginManager
rd /s /q ""{tempDirectory}""
del ""%~f0"" & exit
";
                    File.WriteAllText(batchFilePath, batchContent);

                    // 设置批处理文件的启动信息
                    ProcessStartInfo startInfo = new()
                    {
                        FileName = batchFilePath,
                        UseShellExecute = true,
                        Verb = "runas" // 请求管理员权限
                    };
                    // 启动批处理文件并退出当前程序
                    Process.Start(startInfo);
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"更新失败: {ex.Message}");
                }
            });
        }






        public static void OpenStore()
        {
            PlatformHelper.Open("http://xc213618.ddns.me:9999/D%3A/ColorVision/Projects");
        }
    }
}
