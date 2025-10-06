using ColorVision.Common.MVVM;
using ColorVision.Themes.Controls;
using log4net;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Windows;

namespace ColorVision.UI
{
    public class DownloadFileConfig : IConfig
    {
        public static DownloadFileConfig Instance => ConfigService.Instance.GetRequiredService<DownloadFileConfig>();
        public string Authorization { get; set; } = "1:1";
    }

    public class DownloadFile:ViewModelBase, IUpdate
    {

        private static ILog log = log4net.LogManager.GetLogger(nameof(DownloadFile));

        public string DownloadTile { get; set; }

        public int ProgressValue { get => _ProgressValue; set { _ProgressValue = value; OnPropertyChanged(); } }
        private int _ProgressValue;

        public string SpeedValue { get => _SpeedValue; set { _SpeedValue = value; OnPropertyChanged(); } }
        private string _SpeedValue;

        public string RemainingTimeValue { get => _RemainingTimeValue; set { _RemainingTimeValue = value; OnPropertyChanged(); } }
        private string _RemainingTimeValue;

        public async Task<Version> GetLatestVersionNumber(string url)
        {
            using HttpClient _httpClient = new();
            string versionString = null;
            try
            {

                // If the request is unauthorized, add the authentication header and try again
                var byteArray = Encoding.ASCII.GetBytes(DownloadFileConfig.Instance.Authorization);
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                // You should also consider handling other potential issues here, such as network errors
                versionString = await _httpClient.GetStringAsync(url);

            }
            catch(Exception ex)
            {
                log.Error(ex);
                return new Version();
            }

            // If versionString is still null, it means there was an issue with getting the version number
            if (versionString == null)
            {
                throw new InvalidOperationException("Unable to retrieve version number.");
            }

            return new Version(versionString.Trim());
        }

        public async Task Download(string downloadUrl, string DownloadPath, CancellationToken cancellationToken)
        {
            using (var client = new HttpClient())
            {
                var byteArray = Encoding.ASCII.GetBytes(DownloadFileConfig.Instance.Authorization);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                Stopwatch stopwatch = new();
                var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show($"{Properties.Resources.ErrorOccurred}: {response.ReasonPhrase}");
                    return;
                }

                double totalBytes = response.Content.Headers.ContentLength ?? -1L;
                double totalReadBytes = 0L;
                var readBytes = 0;
                var buffer = new byte[8192];
                var isMoreToRead = true;

                stopwatch.Start();

                using (var fileStream = new FileStream(DownloadPath, FileMode.Create, FileAccess.Write, FileShare.None))
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
                                SpeedValue = $"{Properties.Resources.CurrentSpeed} {speed / 1024 / 1024:F2} MB/s   {totalReadBytes / 1024 / 1024:F2} MB/{totalBytes / 1024 / 1024:F2} MB";

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



    }



}
