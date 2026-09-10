using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.POI;
using System;
using ColorVision.ImageEditor;
using ColorVision.UI;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ColorVision.Engine.Media
{
    public sealed class CvcieMouseProbeOptions : ViewModelBase, IConfig, IImageEditorConfig
    {
        public const string ViewStateKey = "CvcieMouseProbeOptions";

        private static readonly object SyncLock = new();
        private static CvcieMouseProbeOptions? _currentDefaults;

        public static CvcieMouseProbeOptions CurrentDefaults
        {
            get
            {
                if (ConfigService.Instance != null)
                {
                    try
                    {
                        CvcieMouseProbeOptions configBacked = ConfigService.Instance.GetRequiredService<CvcieMouseProbeOptions>();
                        lock (SyncLock)
                        {
                            _currentDefaults = configBacked;
                            return _currentDefaults;
                        }
                    }
                    catch
                    {
                    }
                }

                lock (SyncLock)
                {
                    _currentDefaults ??= new CvcieMouseProbeOptions();
                    return _currentDefaults;
                }
            }
        }

        public static void SaveDefaults()
        {
            try
            {
                ConfigService.Instance?.Save<CvcieMouseProbeOptions>();
            }
            catch
            {
            }
        }

        public static CvcieMouseProbeOptions CreateForView()
        {
            CvcieMouseProbeOptions options = new();
            options.CopyFrom(CurrentDefaults);
            return options;
        }

        public static CvcieMouseProbeOptions GetOrCreate(ImageView imageView)
        {
            if (imageView.Config.Configs.TryGetValue(typeof(CvcieMouseProbeOptions), out var existing) && existing is CvcieMouseProbeOptions current)
                return current;
            if (imageView.Config.Properties.TryGetValue(ViewStateKey, out object? optionsObj) && optionsObj is CvcieMouseProbeOptions options)
            {
                imageView.Config.Configs[typeof(CvcieMouseProbeOptions)] = options;
                return options;
            }

            CvcieMouseProbeOptions created = CreateForView();
            imageView.Config.Configs[typeof(CvcieMouseProbeOptions)] = created;
            imageView.Config.SetViewState(ViewStateKey, created, nameof(CvcieMouseProbeOptions), "当前 CVCIE 视窗的放大镜探针设置");
            return created;
        }

        public void CopyFrom(CvcieMouseProbeOptions source)
        {
            Radius = source.Radius;
            RectWidth = source.RectWidth;
            RectHeight = source.RectHeight;
            MagnigifierType = source.MagnigifierType;
        }

        public PoiMeasurementPoint CreateMeasurementPoint(int x, int y)
        {
            int diameter = Math.Max(1, checked((int)Math.Round(Radius * 2, MidpointRounding.AwayFromZero)));
            return MagnigifierType switch
            {
                MagnigifierType.Circle => new(x, y, diameter, diameter, PoiMeasurementShape.Circle),
                MagnigifierType.Rect => new(x, y, RectWidth, RectHeight, PoiMeasurementShape.Rect),
                MagnigifierType.Ellipse => new(x, y, RectWidth, RectHeight, PoiMeasurementShape.Ellipse),
                _ => throw new ArgumentOutOfRangeException(nameof(MagnigifierType))
            };
        }

        [Display(Name = "Engine_PG_SampleShape", ResourceType = typeof(Properties.Resources))]
        public MagnigifierType MagnigifierType { get => _magnigifierType; set { _magnigifierType = value; OnPropertyChanged(); } }
        private MagnigifierType _magnigifierType = MagnigifierType.Circle;

        [DisplayName("圆半径（px）")]
        [PropertyVisibility(nameof(MagnigifierType), MagnigifierType.Circle)]
        public double Radius { get => _radius; set { if (!double.IsFinite(value) || value < 0.5 || value > 1000000) return; _radius = value; OnPropertyChanged(); } }
        private double _radius = 100;

        [DisplayName("区域宽度（px）")]
        public int RectWidth { get => _rectWidth; set { if (value < 1 || value > 2000000) return; _rectWidth = value; OnPropertyChanged(); } }
        private int _rectWidth = 120;

        [DisplayName("区域高度（px）")]
        public int RectHeight { get => _rectHeight; set { if (value < 1 || value > 2000000) return; _rectHeight = value; OnPropertyChanged(); } }
        private int _rectHeight = 120;
    }
}
