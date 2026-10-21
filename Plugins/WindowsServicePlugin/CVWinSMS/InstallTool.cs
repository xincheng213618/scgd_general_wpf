using ColorVision.UI;
using ColorVision.UI.Menus;
using log4net;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Windows;


namespace WindowsServicePlugin.CVWinSMS
{
    public class InstallTool : MenuItemBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(InstallTool));


        public override string OwnerGuid => "ServiceLog";

        public override string GuidId => "InstallTool";

        public string Name => GetType().Name;   

        public override int Order => 1;

        public override string Header => Properties.Resources.ManagementService;

        internal static string GetText(string key) => Properties.Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

        public string Description => GetDescription();

        public string GetDescription()
        {
            string Description = "打开已配置的服务管理工具；如果不存在，可确认下载并指定保存位置。";
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

        public static CVWinSMSConfig Config => CVWinSMSConfig.Instance;
        public static string LatestReleaseUrl => Config.UpdatePath + "/LATEST_RELEASE";


        public bool ConfigurationStatus { get => _ConfigurationStatus; set { _ConfigurationStatus = value; OnPropertyChanged(); } }
        private bool _ConfigurationStatus = File.Exists(CVWinSMSConfig.Instance.CVWinSMSPath);

        internal static async Task<Version> GetRequiredLatestVersionAsync(Func<Task<Version>> getLatestVersion)
        {
            Version version = await getLatestVersion();
            if (version == null || version <= new Version(0, 0, 0, 0))
                throw new InvalidOperationException(GetText("无法获取有效的服务管理工具版本，请检查网络或更新地址后重试。"));
            return version;
        }

        public async Task Download()
        {
            var downloadFile = new DownloadFile();
            Version version = await GetRequiredLatestVersionAsync(() => downloadFile.GetLatestVersionNumber(LatestReleaseUrl));
            string downloadDir = Environments.DirToolPackageCache;
            string url = $"http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/InstallTool/InstallTool[{version}].zip";

            var service = AssemblyHandler.GetInstance().LoadImplementations<IDownloadService>().FirstOrDefault();
            if (service == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                service.ShowDownloadWindow();
                service.Download(url, downloadDir, DownloadFileConfig.Instance.Authorization, filePath =>
                {
                    if (filePath == null) return;
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        Process.GetProcessesByName("CVWinSMS").ToList().ForEach(p => p.Kill());
                        using (System.Windows.Forms.FolderBrowserDialog folderBrowser = new System.Windows.Forms.FolderBrowserDialog())
                        {
                            folderBrowser.Description = GetText("请选择解压缩目录");
                            folderBrowser.ShowNewFolderButton = true;
                            folderBrowser.RootFolder = Environment.SpecialFolder.Desktop;
                            if (folderBrowser.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                            ZipFile.ExtractToDirectory(filePath, folderBrowser.SelectedPath, true);

                            CVWinSMSConfig.Instance.CVWinSMSPath = folderBrowser.SelectedPath + "\\InstallTool\\CVWinSMS.exe";
                        }

                        ConfigurationStatus = File.Exists(CVWinSMSConfig.Instance.CVWinSMSPath);

                        ProcessStartInfo startInfo = new()
                        {
                            UseShellExecute = true,
                            WorkingDirectory = Environment.CurrentDirectory,
                            FileName = CVWinSMSConfig.Instance.CVWinSMSPath,
                            Verb = "runas"
                        };
                        try
                        {
                            Process.Start(startInfo);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.ToString());
                            File.Delete(filePath);
                        }
                    });
                });
            });
        }

        public override async void Execute()
        {
            await ExecuteMenuActionAsync(ExecuteCoreAsync, ReportMenuFailure);
        }

        internal static async Task ExecuteMenuActionAsync(Func<Task> action, Action<Exception> reportFailure)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                try
                {
                    reportFailure(ex);
                }
                catch (Exception reportException)
                {
                    log.Error("报告服务管理工具启动失败时发生异常。", reportException);
                }
            }
        }

        private static void ReportMenuFailure(Exception ex)
        {
            log.Error("打开或下载服务管理工具失败。", ex);
            MessageBox.Show(
                Application.Current?.GetActiveWindow(),
                string.Format(GetText("打开或下载服务管理工具失败：{0}"), ex.Message),
                "ColorVision",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private async Task ExecuteCoreAsync()
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
                            string? processPath = process.MainModule?.FileName;
                            if (string.IsNullOrWhiteSpace(processPath))
                                continue;
                            CVWinSMSConfig.Instance.CVWinSMSPath = processPath;
                            log.Info($"进程ID: {process.Id}, 文件路径: {CVWinSMSConfig.Instance.CVWinSMSPath}");

                            return;
                        }
                        catch (Exception ex)
                        {
                            log.Debug($"无法获取进程的文件路径: {ex.Message}");
                        }
                    }
                }
                if (MessageBox.Show(Application.Current.GetActiveWindow(), GetText("找不到管理工具，是否下载"), "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    await Download();
                    return;
                }

                if (MessageBox.Show(Application.Current.GetActiveWindow(), GetText("I can't find CVWinSMS (CVWinSMS.exe). Would you like to help me find it?"), GetText("Open in CVWinSMS"), MessageBoxButton.YesNo) == MessageBoxResult.No) return;
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
            startInfo.UseShellExecute = true;
            startInfo.WorkingDirectory = Environment.CurrentDirectory;
            startInfo.FileName = CVWinSMSConfig.Instance.CVWinSMSPath;
            startInfo.Verb = "runas";
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
