using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.UI;
using System.ComponentModel;
using System.Threading.Tasks;

namespace ColorVision.Engine.Media
{
    public sealed class CameraRawBufferCacheConfig : ViewModelBase, IConfig
    {
        public static CameraRawBufferCacheConfig Current => ConfigService.Instance.GetRequiredService<CameraRawBufferCacheConfig>();

        private bool isEnabled;

        [Category("相机取图缓冲"), DisplayName("启用相机取图缓冲")]
        [ConfigSetting(Order = 32, Section = ConfigSettingConstants.SectionFileArchive)]
        [Description("默认关闭，流程加速时可手动开启。开启后每台相机最多保留一块空闲 RAW 缓冲；关闭时释放空闲缓冲，在用图像归还后释放。")]
        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                LocalCameraRawBufferPool.IsCacheEnabled = value;
                if (isEnabled == value) return;
                isEnabled = value;
                OnPropertyChanged();
            }
        }

        public static void SaveCurrent() => ConfigService.Instance.Save<CameraRawBufferCacheConfig>();
    }

    public sealed class CameraRawBufferCacheInitializer : InitializerBase
    {
        public override Task InitializeAsync()
        {
            LocalCameraRawBufferPool.IsCacheEnabled = CameraRawBufferCacheConfig.Current.IsEnabled;
            return Task.CompletedTask;
        }
    }
}
