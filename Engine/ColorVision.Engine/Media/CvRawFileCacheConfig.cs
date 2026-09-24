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

        private bool isEnabled = true;

        [Category("图像文件缓存"), DisplayName("启用 CVRAW 文件缓存")]
        [ConfigSetting(Order = 30, Section = ConfigSettingConstants.SectionFileArchive)]
        [Description("默认开启。关闭后直接读写文件，已有读取完成后释放槽位；显示内存复用保持不变，可用于对比文件缓存效果。")]
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

        public static void SaveCurrent()
        {
            ConfigService.Instance.Save<CvRawFileCacheConfig>();
        }
    }

    public sealed class CvRawFileCacheInitializer : InitializerBase
    {
        public override Task InitializeAsync()
        {
            CVFileReadCache.IsEnabled = CvRawFileCacheConfig.Current.IsEnabled;
            return Task.CompletedTask;
        }
    }

}
