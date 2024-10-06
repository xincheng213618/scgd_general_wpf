#pragma warning disable SYSLIB0014
using ColorVision.Common.Utilities;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Windows;

namespace WindowsServicePlugin
{
    public class CVWinSMSConfig : IConfig
    {
        public static CVWinSMSConfig Instance => ConfigService.Instance.GetRequiredService<CVWinSMSConfig>();

        public string CVWinSMSPath { get => _CVWinSMSPath; set => _CVWinSMSPath = value; }
        private string _CVWinSMSPath = string.Empty;
    }

    public class InstallTool : MenuItemBase, IWizardStep, IUpdate
    {
        public override string OwnerGuid => "ServiceLog";

        public override string GuidId => "InstallTool";

        public override int Order => 99;

        public override string Header => Properties.Resources.ManagementService;
        public string DownloadTile { get; set; } = "下载服务管理工具";

        private string url = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/InstallTool/InstallTool[2.0.0.24092].zip";
        private string downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + @"ColorVision\\InstallTool[2.0.0.24092].zip";

        public int ProgressValue { get => _ProgressValue; set { _ProgressValue = value; NotifyPropertyChanged(); } }
        private int _ProgressValue;

        public string SpeedValue { get => _SpeedValue; set { _SpeedValue = value; NotifyPropertyChanged(); } }
        private string _SpeedValue;

        public string RemainingTimeValue { get => _RemainingTimeValue; set { _RemainingTimeValue = value; NotifyPropertyChanged(); } }
        private string _RemainingTimeValue;

        bool IsPassWorld;

        public async Task GetIsPassWorld()
        {
            string url = "http://xc213618.ddns.me:9999/D%3A/LATEST_RELEASE";
            using HttpClient _httpClient = new();
            string versionString = null;
            try
            {
                versionString = await _httpClient.GetStringAsync(url);
            }
            catch (HttpRequestException e) when (e.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                IsPassWorld = true;
            }
        }

        private async Task Download(string downloadUrl, string DownloadPath, CancellationToken cancellationToken)
        {
            using (var client = new HttpClient())
            {
                if (IsPassWorld)
                {
                    string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{1}:{1}"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                }
                Stopwatch stopwatch = new();
                var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show($"{Properties.Resources.ErrorOccurred}: {response.ReasonPhrase}");
                    return;
                }

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var totalReadBytes = 0L;
                var readBytes = 0;
                var buffer = new byte[8192];
                var isMoreToRead = true;

                stopwatch.Start();

                using (var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
                {
                    do
                    {
                        readBytes = await stream.ReadAsync(buffer, cancellationToken);
                        if (readBytes == 0)
                        {
                            isMoreToRead = false;
                        }
                        else
                        {
                            await fileStream.WriteAsync(buffer.AsMemory(0, readBytes), cancellationToken);

                            totalReadBytes += readBytes;
                            int progressPercentage = totalBytes != -1L
                                ? (int)((totalReadBytes * 100) / totalBytes)
                                : -1;

                            ProgressValue = progressPercentage;

                            if (cancellationToken.IsCancellationRequested)
                            {
                                return;
                            }

                            if (stopwatch.ElapsedMilliseconds > 200) // Update speed at least once per second
                            {
                                double speed = totalReadBytes / stopwatch.Elapsed.TotalSeconds;
                                SpeedValue = $"{Properties.Resources.CurrentSpeed} {speed / 1024 / 1024:F2} MB/s";

                                if (totalBytes != -1L)
                                {
                                    double remainingBytes = totalBytes - totalReadBytes;
                                    double remainingTime = remainingBytes / speed; // in seconds
                                    RemainingTimeValue = $"{Properties.Resources.TimeLeft} {TimeSpan.FromSeconds(remainingTime):hh\\:mm\\:ss}";
                                }
                            }
                        }
                    }
                    while (isMoreToRead);
                }
                stopwatch.Stop();
            }
        }

        public void Download()
        {
            WindowUpdate windowUpdate = new WindowUpdate(this);
            if (!File.Exists(downloadPath))
            {
                windowUpdate.Show();
            }

            Task.Run(async () =>
            {
                if (!File.Exists(downloadPath))
                {
                    await GetIsPassWorld();
                    CancellationTokenSource _cancellationTokenSource = new();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        windowUpdate.Show();
                    });
                    await Download(url, downloadPath, _cancellationTokenSource.Token);
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
