using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;

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

        [DisplayName("Gray32Float 转为 Gray16")]
        [Description("启用后，打开 Gray32Float TIFF 时按当前图像最小值和最大值归一化后转换为 Gray16；关闭后保留原始 Gray32Float 图像。")]
        public bool ConvertGray32FloatToGray16OnOpen
        {
            get => _convertGray32FloatToGray16OnOpen;
            set
            {
                _convertGray32FloatToGray16OnOpen = value;
                OnPropertyChanged();
            }
        }
        private bool _convertGray32FloatToGray16OnOpen = true;
    }
}