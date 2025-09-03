#pragma warning disable CS8604,CA1822
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using log4net;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Projects
{
    public class ProjectWindowConfig : WindowConfig
    {

    }

    public class ProjectManager: ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ProjectManager));
        private static ProjectManager _instance;
        private static readonly object _locker = new();
        public static ProjectManager GetInstance() { lock (_locker) { _instance ??= new ProjectManager(); return _instance; } }
        public ObservableCollection<ProjectInfo> Projects { get; private set; } = new ObservableCollection<ProjectInfo>();

        public ProjectWindowConfig Config => ConfigService.Instance.GetRequiredService<ProjectWindowConfig>();
        public RelayCommand EditConfigCommand { get; set; }

        public RelayCommand OpenStoreCommand { get;  set; }
        public RelayCommand OpenDownloadCacheCommand { get; set; }

        public RelayCommand InstallPackageCommand { get; set; }
        public RelayCommand DownloadPackageCommand { get; set; }

        private DownloadFile DownloadFile { get; set; }
        public ProjectManager()
        {
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                try
                {
                    foreach (Type type in assembly.GetTypes().Where(t => typeof(IProject).IsAssignableFrom(t) && !t.IsAbstract))
                    {
                        if (Activator.CreateInstance(type) is IProject project)
                        {
                            ProjectInfo info = new ProjectInfo(project, assembly);
                            info.AssemblyVersion = assembly.GetName().Version;
                            info.AssemblyBuildDate = File.GetLastWriteTime(assembly.Location);
                            Projects.Add(info);
                        }
                    }
                }catch(Exception ex)
                {
                    log.Error(ex);
                }


            }
            OpenStoreCommand = new RelayCommand(a => OpenStore());
            InstallPackageCommand = new RelayCommand(a => InstallPackage());
            DownloadPackageCommand = new RelayCommand(a => DownloadPackage());
            DownloadFile = new DownloadFile();
            EditConfigCommand = new RelayCommand(a => new PropertyEditorWindow(Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            OpenDownloadCacheCommand = new RelayCommand(a => OpenDownloadCache());
        }
        public static void OpenDownloadCache()
        {
            PlatformHelper.OpenFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"ColorVision"));
        }

        public string SearchName { get => _SearchName; set { _SearchName = value; OnPropertyChanged(); }}
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
                    WindowUpdate windowUpdate = new WindowUpdate(DownloadFile) { WindowStartupLocation = WindowStartupLocation.CenterOwner };
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
title ""更新脚本""
taskkill /f /im ""{executableName}""
timeout /t 0
xcopy /y /e ""{tempDirectory}\*"" ""{programPluginsDirectory}""
start """" ""{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, executableName)}"" -c MenuProjectManager
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
title ""更新脚本""
taskkill /f /im ""{executableName}""
timeout /t 0
xcopy /y /e ""{tempDirectory}\*"" ""{programPluginsDirectory}""
start """" ""{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, executableName)}"" -c MenuProjectManager
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


        public void CreateShortCut()
        {

            foreach (var item in Projects)
            {
                string GetExecutablePath = Environments.GetExecutablePath();
                string shortcutName = item.Project.Header;
                string shortcutPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string arguments = $"-project {shortcutName}";
                if(shortcutName!=null)
                    Common.NativeMethods.ShortcutCreator.CreateShortcut(shortcutName, shortcutPath, GetExecutablePath, arguments);
            }

        }
    }
}
