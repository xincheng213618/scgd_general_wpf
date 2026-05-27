using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Properties;
using ColorVision.UI;

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

        [LocalizedDisplayName(typeof(Resources), nameof(Properties.Resources.TifOpenConfig_ConvertToGray16_DisplayName))]
        [LocalizedDescription(typeof(Resources), nameof(Properties.Resources.TifOpenConfig_ConvertToGray16_Description))]
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