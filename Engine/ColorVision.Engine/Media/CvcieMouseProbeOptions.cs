using ColorVision.Common.MVVM;
using ColorVision.ImageEditor;
using ColorVision.UI;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ColorVision.Engine.Media
{
    public sealed class CvcieMouseProbeOptions : ViewModelBase, IConfig
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
            if (imageView.Config.Properties.TryGetValue(ViewStateKey, out object? optionsObj) && optionsObj is CvcieMouseProbeOptions options)
            {
                return options;
            }

            CvcieMouseProbeOptions created = CreateForView();
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

        [Display(Name = "Engine_PG_SampleShape", Description = "Engine_PG_SampleShapeDesc", ResourceType = typeof(Properties.Resources))]
        public MagnigifierType MagnigifierType { get => _magnigifierType; set { _magnigifierType = value; OnPropertyChanged(); } }
        private MagnigifierType _magnigifierType = MagnigifierType.Circle;

        [Display(Name = "Engine_PG_SampleRadius", Description = "Engine_PG_SampleRadiusDesc", ResourceType = typeof(Properties.Resources))]
        public double Radius { get => _radius; set { _radius = value; OnPropertyChanged(); } }
        private double _radius = 100;

        [Display(Name = "Engine_PG_RectWidth", Description = "Engine_PG_RectWidthDesc", ResourceType = typeof(Properties.Resources))]
        public int RectWidth { get => _rectWidth; set { _rectWidth = value; OnPropertyChanged(); } }
        private int _rectWidth = 120;

        [Display(Name = "Engine_PG_RectHeight", Description = "Engine_PG_RectHeightDesc", ResourceType = typeof(Properties.Resources))]
        public int RectHeight { get => _rectHeight; set { _rectHeight = value; OnPropertyChanged(); } }
        private int _rectHeight = 120;
    }
}