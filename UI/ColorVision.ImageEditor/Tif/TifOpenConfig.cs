using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Tif
{
    public class TifOpenConfig : ViewModelBase, IConfig
    {
        private static readonly object SyncLock = new();
        private static TifOpenConfig? _current;

        public static TifOpenConfig Current
        {
            get
            {
                if (ConfigService.Instance != null)
                {
                    try
                    {
                        TifOpenConfig configBacked = ConfigService.Instance.GetRequiredService<TifOpenConfig>();
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
                    _current ??= new TifOpenConfig();
                    return _current;
                }
            }
        }

        public static void SaveCurrent()
        {
            try
            {
                ConfigService.Instance?.Save<TifOpenConfig>();
            }
            catch
            {
            }
        }

        [DisplayName("Gray32Float 覆盖缩放模式")]
        [Description("启用后，打开 Gray32Float TIFF 时会临时覆盖当前图像缩放模式；关闭则回到 ImageView 默认缩放模式。")]
        public bool OverrideBitmapScalingModeForGray32Float
        {
            get => _overrideBitmapScalingModeForGray32Float;
            set
            {
                _overrideBitmapScalingModeForGray32Float = value;
                OnPropertyChanged();
            }
        }
        private bool _overrideBitmapScalingModeForGray32Float = true;

        [DisplayName("Gray32Float 缩放模式")]
        [Description("当启用覆盖时，Gray32Float TIFF 在 ImageView 中使用的 BitmapScalingMode。")]
        public BitmapScalingMode Gray32FloatBitmapScalingMode
        {
            get => _gray32FloatBitmapScalingMode;
            set
            {
                _gray32FloatBitmapScalingMode = value;
                OnPropertyChanged();
            }
        }
        private BitmapScalingMode _gray32FloatBitmapScalingMode = BitmapScalingMode.NearestNeighbor;
    }
}