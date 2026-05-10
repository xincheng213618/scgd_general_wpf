using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Settings
{
    public class DefaultBitmapScalingConfig : ViewModelBase, IConfig
    {
        private static readonly object SyncLock = new();
        private static DefaultBitmapScalingConfig? _current;

        public static DefaultBitmapScalingConfig Current
        {
            get
            {
                if (ConfigService.Instance != null)
                {
                    try
                    {
                        var configBacked = ConfigService.Instance.GetRequiredService<DefaultBitmapScalingConfig>();
                        lock (SyncLock)
                        {
                            _current = configBacked;
                            return _current;
                        }
                    }
                    catch
                    {
                    }
                }

                lock (SyncLock)
                {
                    _current ??= new DefaultBitmapScalingConfig();
                    return _current;
                }
            }
        }

        public static void SaveCurrent()
        {
            try
            {
                ConfigService.Instance?.Save<DefaultBitmapScalingConfig>();
            }
            catch
            {
            }
        }

        [DisplayName("默认图像缩放模式")]
        public BitmapScalingMode DefaultBitmapScalingMode
        {
            get => _defaultBitmapScalingMode;
            set
            {
                _defaultBitmapScalingMode = value;
                OnPropertyChanged();
            }
        }
        private BitmapScalingMode _defaultBitmapScalingMode = BitmapScalingMode.HighQuality;
    }
}