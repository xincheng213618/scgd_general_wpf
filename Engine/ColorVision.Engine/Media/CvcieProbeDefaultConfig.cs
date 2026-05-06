using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.UI;
using System.ComponentModel;

namespace ColorVision.Engine.Media
{
    public sealed class CvcieProbeDefaultConfig : ViewModelBase, IConfig
    {
        private static readonly object SyncLock = new();
        private static CvcieProbeDefaultConfig? _current;

        public static CvcieProbeDefaultConfig Current
        {
            get
            {
                if (ConfigService.Instance != null)
                {
                    try
                    {
                        CvcieProbeDefaultConfig configBacked = ConfigService.Instance.GetRequiredService<CvcieProbeDefaultConfig>();
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
                    _current ??= new CvcieProbeDefaultConfig();
                    return _current;
                }
            }
        }

        public static void SaveCurrent()
        {
            try
            {
                ConfigService.Instance?.Save<CvcieProbeDefaultConfig>();
            }
            catch
            {
            }
        }

        [DisplayName("取样半径")]
        [Description("新打开 CVCIE 图像时，Circle 模式的默认半径。")]
        public double Radius { get => _radius; set { _radius = value; OnPropertyChanged(); } }
        private double _radius = 100;

        [DisplayName("矩形宽度")]
        [Description("新打开 CVCIE 图像时，Rect 模式的默认矩形宽度。")]
        public int RectWidth { get => _rectWidth; set { _rectWidth = value; OnPropertyChanged(); } }
        private int _rectWidth = 120;

        [DisplayName("矩形高度")]
        [Description("新打开 CVCIE 图像时，Rect 模式的默认矩形高度。")]
        public int RectHeight { get => _rectHeight; set { _rectHeight = value; OnPropertyChanged(); } }
        private int _rectHeight = 120;

        [DisplayName("取样类型")]
        [Description("新打开 CVCIE 图像时使用的默认取样形状。")]
        public MagnigifierType MagnigifierType { get => _magnigifierType; set { _magnigifierType = value; OnPropertyChanged(); } }
        private MagnigifierType _magnigifierType = MagnigifierType.Circle;
    }
}