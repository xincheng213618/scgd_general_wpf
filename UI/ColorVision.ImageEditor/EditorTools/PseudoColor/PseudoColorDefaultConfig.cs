using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.UI;
using System.ComponentModel;

namespace ColorVision.ImageEditor.EditorTools.PseudoColor
{
    public class PseudoColorDefaultConfig : ViewModelBase, IConfig
    {
        private static readonly object SyncLock = new();
        private static PseudoColorDefaultConfig? _current;

        public static PseudoColorDefaultConfig Current
        {
            get
            {
                if (ConfigService.Instance != null)
                {
                    try
                    {
                        var configBacked = ConfigService.Instance.GetRequiredService<PseudoColorDefaultConfig>();
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
                    _current ??= new PseudoColorDefaultConfig();
                    return _current;
                }
            }
        }

        public static void SaveCurrent()
        {
            try
            {
                ConfigService.Instance?.Save<PseudoColorDefaultConfig>();
            }
            catch
            {
            }
        }

        [DisplayName("默认伪彩色类型")]
        public ColormapTypes DefaultColormapTypes
        {
            get => _defaultColormapTypes;
            set
            {
                _defaultColormapTypes = value;
                OnPropertyChanged();
            }
        }
        private ColormapTypes _defaultColormapTypes = ColormapTypes.COLORMAP_JET;

        [DisplayName("默认自动范围")]
        public bool IsAutoSetRangeByDefault
        {
            get => _isAutoSetRangeByDefault;
            set
            {
                _isAutoSetRangeByDefault = value;
                OnPropertyChanged();
            }
        }
        private bool _isAutoSetRangeByDefault;
    }
}