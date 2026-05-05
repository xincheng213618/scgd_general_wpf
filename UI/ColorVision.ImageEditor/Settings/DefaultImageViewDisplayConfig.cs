using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;

namespace ColorVision.ImageEditor.Settings
{
    public class DefaultImageViewDisplayConfig : ViewModelBase, IConfig
    {
        private static readonly object SyncLock = new();
        private static DefaultImageViewDisplayConfig? _current;

        public static DefaultImageViewDisplayConfig Current
        {
            get
            {
                if (ConfigService.Instance != null)
                {
                    try
                    {
                        var configBacked = ConfigService.Instance.GetRequiredService<DefaultImageViewDisplayConfig>();
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
                    _current ??= new DefaultImageViewDisplayConfig();
                    return _current;
                }
            }
        }

        public static void SaveCurrent()
        {
            try
            {
                ConfigService.Instance?.Save<DefaultImageViewDisplayConfig>();
            }
            catch
            {
            }
        }

        [DisplayName("最大缩放")]
        [Description("Zoombox 的全局最大缩放倍率。所有 ImageView 默认共用此值。")]
        public double MaxZoom
        {
            get => _maxZoom;
            set
            {
                double normalized = value < _minZoom ? _minZoom : value;
                if (_maxZoom == normalized)
                {
                    return;
                }

                _maxZoom = normalized;
                OnPropertyChanged();
            }
        }
        private double _maxZoom = 100;

        [DisplayName("最小缩放")]
        [Description("Zoombox 的全局最小缩放倍率。所有 ImageView 默认共用此值。")]
        public double MinZoom
        {
            get => _minZoom;
            set
            {
                _minZoom =  value;
                OnPropertyChanged();
            }
        }
        private double _minZoom = 0.005;

        [DisplayName("像素值显示最小像素格")]
        [Description("NearestNeighbor 下单个像素映射到屏幕上的最小尺寸。值越小越早显示像素值，但重绘成本越高。为避免卡顿，最小允许值为 100。")]
        public double PixelValueOverlayMinPixelCellSize
        {
            get => _pixelValueOverlayMinPixelCellSize;
            set
            {
                _pixelValueOverlayMinPixelCellSize = value;
                OnPropertyChanged();
            }
        }
        private double _pixelValueOverlayMinPixelCellSize = 50;

        [DisplayName("像素值显示最大可见像素数")]
        [Description("可同时绘制文字的最大像素数量，超过后隐藏 overlay，避免 CopyPixels 和文本重绘过重。")]
        public int PixelValueOverlayMaxVisiblePixelCount
        {
            get => _pixelValueOverlayMaxVisiblePixelCount;
            set
            {
                int normalized = value < 1 ? 1 : value;
                if (_pixelValueOverlayMaxVisiblePixelCount == normalized)
                {
                    return;
                }

                _pixelValueOverlayMaxVisiblePixelCount = normalized;
                OnPropertyChanged();
            }
        }
        private int _pixelValueOverlayMaxVisiblePixelCount = 2200;
    }
}