#pragma warning disable CS8604
using ColorVision.Common.MVVM;
using ColorVision.Properties;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using log4net;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Plugins
{
    public class PluginInfo:ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PluginInfo));

        public ContextMenu ContextMenu { get; set; }

        public IPlugin Plugin { get; set; }
        public Version? AssemblyVersion { get; set; }
        public DateTime? AssemblyBuildDate { get; set; }
        public string? AssemblyName { get; set; }
        public string? AssemblyPath { get; set; }
        public string? AssemblyCulture { get; set; }
        public string? AssemblyPublicKeyToken { get; set; }
        public string? PackageName { get; set; }

        public Version LastVersion { get => _LastVersion; set { _LastVersion = value; NotifyPropertyChanged(); } }
        private Version _LastVersion;

        public RelayCommand DeleteCommand { get; set; }
        public RelayCommand UpdateCommand { get; set; }
        DownloadFile DownloadFile { get; set; }
        public PluginInfo()
        {
        }
        public PluginInfo(IPlugin plugin, Assembly assembly)
        {
            Plugin = plugin;
            try
            {
                AssemblyName = assembly.GetName().Name;
                AssemblyVersion = assembly.GetName().Version;
                AssemblyBuildDate = File.GetLastWriteTime(assembly.Location);
                AssemblyPath = assembly.Location;
                AssemblyCulture = assembly.GetName().CultureInfo?.Name ?? "neutral";
                AssemblyPublicKeyToken = BitConverter.ToString(assembly.GetName().GetPublicKeyToken() ?? Array.Empty<byte>());
                PackageName = Path.GetFileNameWithoutExtension(assembly.Location);
            }
            catch (Exception ex)
            {
                // 记录错误日志
                LogManager.GetLogger(typeof(PluginInfo)).Error("Error retrieving assembly info", ex);
            }

            DeleteCommand = new RelayCommand(a => Delete());
            UpdateCommand = new RelayCommand(a => Update());
            ContextMenu = new ContextMenu();

            DownloadFile = new DownloadFile();
            DownloadFile.DownloadTile = ColorVision.Properties.Resources.Update + Plugin.Header;
            Task.Run(() => CheckVersion());

            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Delete, Command = ApplicationCommands.Delete });
            ContextMenu.Items.Add(new MenuItem() { Header = ColorVision.Properties.Resources.Update, Command = UpdateCommand });
        }

        public async void CheckVersion()
        {
            string LatestReleaseUrl = UpdateUrl + "/" + PackageName + "/LATEST_RELEASE";
            LastVersion = await DownloadFile.GetLatestVersionNumber(LatestReleaseUrl);
        }


        string UpdateUrl = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Plugins";
        public async void Update()
        {
            string LatestReleaseUrl = UpdateUrl + "/" + PackageName + "/LATEST_RELEASE";
            Version version = await DownloadFile.GetLatestVersionNumber(LatestReleaseUrl);
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), "是否更新", Plugin.Header, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    string downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + $"ColorVision\\{PackageName}-{version}.zip";
                    string url = $"{UpdateUrl}/{PackageName}/{PackageName}-{version}.zip";
                    WindowUpdate windowUpdate = new WindowUpdate(DownloadFile) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
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

                                string targetPluginDirectory = Path.Combine(programPluginsDirectory, PackageName);

                                string? executableName = Path.GetFileName(Environment.ProcessPath);

                                string batchContent = $@"
@echo off
taskkill /f /im ""{executableName}""
timeout /t 0
xcopy /y /e ""{tempDirectory}\*"" ""{programPluginsDirectory}""
start """" ""{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, executableName)}"" -c MenuPluginManager
rd /s /q ""{tempDirectory}""
del ""%~f0"" & exit
";
                                File.WriteAllText(batchFilePath, batchContent);

                                // 设置批处理文件的启动信息
                                ProcessStartInfo startInfo = new()
                                {
                                    FileName = batchFilePath,
                                    UseShellExecute = true,
                                    WindowStyle = ProcessWindowStyle.Hidden
                                };
                                if (Environment.CurrentDirectory.Contains("C:\\Program Files"))
                                {
                                    startInfo.Verb = "runas"; // 请求管理员权限
                                    startInfo.WindowStyle = ProcessWindowStyle.Normal;
                                }
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

        public void Delete()
        {
            if (MessageBox.Show(Application.Current.GetActiveWindow(), $"是否确认删除插件{Plugin.Header}", Resources.PluginManagerWindow, MessageBoxButton.YesNo) == MessageBoxResult.No) return;

            string tempDirectory = Path.Combine(Path.GetTempPath(), "ColorVisionPluginsUpdate");
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }

            Directory.CreateDirectory(tempDirectory);
            // 创建批处理文件内容
            string batchFilePath = Path.Combine(tempDirectory, "update.bat");
            string programPluginsDirectory = AppDomain.CurrentDomain.BaseDirectory + "Plugins";

            string targetPluginDirectory = Path.Combine(programPluginsDirectory, PackageName);

            string? executableName = Path.GetFileName(Environment.ProcessPath);

            string batchContent = $@"
@echo off
taskkill /f /im ""{executableName}""
timeout /t 0
setlocal

rem 设置要删除的目录路径
set targetDirectory=""{targetPluginDirectory}""

rem 检查目录是否存在
if exist %targetDirectory% (
    echo 正在删除目录: %targetDirectory%
    rd /s /q %targetDirectory%
    echo 删除完成。
) else (
    echo 目录不存在: %targetDirectory%
)

endlocal
start """" ""{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, executableName)}"" -c MenuPluginManager
rd /s /q ""{tempDirectory}""
del ""%~f0"" & exit
";
            File.WriteAllText(batchFilePath, batchContent);

            // 设置批处理文件的启动信息
            ProcessStartInfo startInfo = new()
            {
                FileName = batchFilePath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            if (Environment.CurrentDirectory.Contains("C:\\Program Files"))
            {
                startInfo.Verb = "runas"; // 请求管理员权限
                startInfo.WindowStyle = ProcessWindowStyle.Normal;
            }
            Process.Start(startInfo);
            Environment.Exit(0);
        }
    }
}
