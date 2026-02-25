using ColorVision.UI;
using System.Collections.Generic;

namespace ColorVision.UI.Desktop.Download
{
    public class DownloadManagerConfigProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>
            {
                new ConfigSettingMetadata
                {
                    Order = 1,
                    BindingName = nameof(DownloadManagerConfig.MaxConcurrentTasks),
                    Source = DownloadManagerConfig.Instance,
                },
                new ConfigSettingMetadata
                {
                    Order = 2,
                    BindingName = nameof(DownloadManagerConfig.DefaultDownloadPath),
                    Source = DownloadManagerConfig.Instance,
                },
                new ConfigSettingMetadata
                {
                    Order = 3,
                    BindingName = nameof(DownloadManagerConfig.EnableSpeedLimit),
                    Source = DownloadManagerConfig.Instance,
                },
                new ConfigSettingMetadata
                {
                    Order = 4,
                    BindingName = nameof(DownloadManagerConfig.SpeedLimitMB),
                    Source = DownloadManagerConfig.Instance,
                },
                new ConfigSettingMetadata
                {
                    Order = 5,
                    BindingName = nameof(DownloadManagerConfig.ShowCompletedNotification),
                    Source = DownloadManagerConfig.Instance,
                },
                new ConfigSettingMetadata
                {
                    Order = 6,
                    BindingName = nameof(DownloadManagerConfig.RunFileAfterDownload),
                    Source = DownloadManagerConfig.Instance,
                }
            };
        }
    }
}
