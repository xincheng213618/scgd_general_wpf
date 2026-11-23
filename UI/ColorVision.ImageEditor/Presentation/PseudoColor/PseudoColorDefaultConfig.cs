using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.ImageEditor.Settings;
using ColorVision.UI;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

// ConfigHandler persists Type.FullName; keep this identity so saved defaults remain readable.
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

        [Display(Name = nameof(SettingsText.PseudoAutoRange), Description = nameof(SettingsText.PseudoAutoRangeDescription), ResourceType = typeof(SettingsText))]
        public bool IsAutoSetRangeByDefault
        {
            get => _isAutoSetRangeByDefault;
            set
            {
                _isAutoSetRangeByDefault = value;
                OnPropertyChanged();
            }
        }
        private bool _isAutoSetRangeByDefault = true;
    }
}
