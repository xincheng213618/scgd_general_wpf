using ColorVision.Common.MVVM;
using ColorVision.FileIO;
using ColorVision.UI;
using System.ComponentModel;
using System.Threading.Tasks;

namespace ColorVision.Engine.Media
{
    public sealed class CvRawFileCacheConfig : ViewModelBase, IConfig
    {
        public static CvRawFileCacheConfig Current => ConfigService.Instance.GetRequiredService<CvRawFileCacheConfig>();

        private bool isEnabled;
        private int maximumEntries = 1;

        [Category("图像文件缓存"), DisplayName("启用 CVRAW 文件缓存")]
        [ConfigSetting(Order = 30, Section = ConfigSettingConstants.SectionFileArchive)]
        [Description("默认关闭，流程加速时可手动开启。关闭后读写照常执行，不保留空闲缓冲；在用缓冲归还后释放。")]
        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                CVFileReadCache.IsEnabled = value;
                if (isEnabled == value) return;
                isEnabled = value;
                OnPropertyChanged();
            }
        }

        [Category("图像文件缓存"), DisplayName("图像缓存数量上限")]
        [ConfigSetting(Order = 31, Section = ConfigSettingConstants.SectionFileArchive)]
        [Description("默认保留 1 个完整 CVRAW 图像文件，可调整；减少数量后释放多余缓存，磁盘文件保留。")]
        public int MaximumEntries
        {
            get => maximumEntries;
            set
            {
                CVFileReadCache.MaximumEntries = value;
                if (maximumEntries == value) return;
                maximumEntries = value;
                OnPropertyChanged();
            }
        }

        public static void SaveCurrent()
        {
            ConfigService.Instance.Save<CvRawFileCacheConfig>();
        }
    }

    public sealed class CvRawFileCacheInitializer : InitializerBase
    {
        public override Task InitializeAsync()
        {
            CVFileReadCache.MaximumEntries = CvRawFileCacheConfig.Current.MaximumEntries;
            CVFileReadCache.IsEnabled = CvRawFileCacheConfig.Current.IsEnabled;
            return Task.CompletedTask;
        }
    }

}
