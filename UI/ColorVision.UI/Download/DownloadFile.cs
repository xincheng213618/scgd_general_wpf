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

    public class DownloadFile:ViewModelBase
    {
        private static ILog log = log4net.LogManager.GetLogger(nameof(DownloadFile));

        public async Task<Version> GetLatestVersionNumber(string url)
        {
            using HttpClient _httpClient = new();
            string versionString = null;
            try
            {
                var byteArray = Encoding.ASCII.GetBytes(DownloadFileConfig.Instance.Authorization);
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                versionString = await _httpClient.GetStringAsync(url);
            }
            catch(Exception ex)
            {
                log.Error(ex);
                return new Version();
            }

            if (versionString == null)
            {
                throw new InvalidOperationException("Unable to retrieve version number.");
            }

            return new Version(versionString.Trim());
        }


    }



}
