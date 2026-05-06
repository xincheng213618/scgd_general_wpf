using ColorVision.Common.MVVM;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw.Special;
using System.ComponentModel;

namespace ColorVision.Engine.Media
{
    public sealed class CvcieProbeSettings : ViewModelBase
    {
        public const string PropertyKey = "CvcieProbeSettings";

        public static CvcieProbeSettings CreateFromDefaults()
        {
            CvcieProbeSettings settings = new();
            settings.ApplyDefaults(CvcieProbeDefaultConfig.Current);
            return settings;
        }

        public static CvcieProbeSettings GetOrCreate(ImageView imageView)
        {
            if (imageView.Config.Properties.TryGetValue(PropertyKey, out object? settingsObj) && settingsObj is CvcieProbeSettings settings)
            {
                return settings;
            }

            CvcieProbeSettings created = CreateFromDefaults();
            imageView.Config.AddProperties(PropertyKey, created);
            return created;
        }

        public static bool TryGet(ImageView imageView, out CvcieProbeSettings? settings)
        {
            if (imageView.Config.Properties.TryGetValue(PropertyKey, out object? settingsObj) && settingsObj is CvcieProbeSettings typedSettings)
            {
                settings = typedSettings;
                return true;
            }

            settings = null;
            return false;
        }

        public void ApplyDefaults(CvcieProbeDefaultConfig defaults)
        {
            Radius = defaults.Radius;
            RectWidth = defaults.RectWidth;
            RectHeight = defaults.RectHeight;
            MagnigifierType = defaults.MagnigifierType;
        }

        [DisplayName("取样半径")]
        [Description("Circle 取样时使用的半径。")]
        public double Radius { get => _radius; set { _radius = value; OnPropertyChanged(); } }
        private double _radius = 100;

        [DisplayName("矩形宽度")]
        [Description("Rect 取样时使用的矩形宽度。")]
        public int RectWidth { get => _rectWidth; set { _rectWidth = value; OnPropertyChanged(); } }
        private int _rectWidth = 120;

        [DisplayName("矩形高度")]
        [Description("Rect 取样时使用的矩形高度。")]
        public int RectHeight { get => _rectHeight; set { _rectHeight = value; OnPropertyChanged(); } }
        private int _rectHeight = 120;

        [DisplayName("取样类型")]
        [Description("控制探针按圆形还是矩形范围取样。")]
        public MagnigifierType MagnigifierType { get => _magnigifierType; set { _magnigifierType = value; OnPropertyChanged(); } }
        private MagnigifierType _magnigifierType = MagnigifierType.Circle;
    }
}
