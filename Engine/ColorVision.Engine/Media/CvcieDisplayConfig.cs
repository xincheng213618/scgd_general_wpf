using ColorVision.Common.MVVM;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using System;
using System.ComponentModel;

namespace ColorVision.Engine.Media
{
    public enum CvcieDisplayMode
    {
        [Description("原图（CVRAW）")]
        Source,
        [Description("真彩 sRGB（XYZ）")]
        Srgb,
    }

    public enum CvcieBrightnessMode
    {
        [Description("自动适配")]
        Auto,
        [Description("固定参考白亮度")]
        ReferenceWhite,
    }

    public sealed class CvcieDisplayConfig : ViewModelBase, IConfig
    {
        private static readonly CvcieDisplayConfig Fallback = new();
        private static readonly ILog Log = LogManager.GetLogger(typeof(CvcieDisplayConfig));

        public static CvcieDisplayConfig Current
        {
            get
            {
                try
                {
                    return ConfigService.Instance?.GetRequiredService<CvcieDisplayConfig>() ?? Fallback;
                }
                catch (Exception ex)
                {
                    Log.Warn("读取 CVCIE 全局显示设置失败，使用默认设置。", ex);
                    return Fallback;
                }
            }
        }

        public static void SaveCurrent()
        {
            try
            {
                ConfigService.Instance?.Save<CvcieDisplayConfig>();
            }
            catch (Exception ex)
            {
                Log.Warn("保存 CVCIE 全局显示设置失败。", ex);
            }
        }

        [Category("CVCIE 显示"), DisplayName("启用真彩显示")]
        [Description("启用后，新打开的 CVCIE 默认显示 XYZ 真彩 sRGB；关闭后默认显示原图。图层临时切换不改变此开关，转换异常只记日志并回退原图或 Y 灰度。")]
        [JsonIgnore]
        public bool EnableTrueColor
        {
            get => DisplayMode == CvcieDisplayMode.Srgb;
            set => DisplayMode = value ? CvcieDisplayMode.Srgb : CvcieDisplayMode.Source;
        }

        // Keep the existing persisted value so older saved settings retain their behavior.
        [Browsable(false)]
        public CvcieDisplayMode DisplayMode
        {
            get => _displayMode;
            set
            {
                _displayMode = Enum.IsDefined(value) ? value : CvcieDisplayMode.Source;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EnableTrueColor));
            }
        }
        private CvcieDisplayMode _displayMode;

        [Category("CVCIE 显示"), DisplayName("真彩亮度映射")]
        [Description("自动适配对整幅图使用同一个亮度系数；固定参考白亮度用于跨图比较。")]
        public CvcieBrightnessMode BrightnessMode
        {
            get => _brightnessMode;
            set { _brightnessMode = Enum.IsDefined(value) ? value : CvcieBrightnessMode.Auto; OnPropertyChanged(); }
        }
        private CvcieBrightnessMode _brightnessMode;

        [Category("CVCIE 显示"), DisplayName("参考白亮度")]
        [Description("与 CVCIE 的 Y 使用相同单位（通常为 cd/m²）。此亮度的 D65 白映射到 sRGB 白；必须大于 0。")]
        [PropertyVisibility(nameof(BrightnessMode), CvcieBrightnessMode.ReferenceWhite)]
        public double ReferenceWhiteLuminance
        {
            get => _referenceWhiteLuminance;
            set
            {
                if (!double.IsFinite(value) || value <= 0) return;
                _referenceWhiteLuminance = value;
                OnPropertyChanged();
            }
        }
        private double _referenceWhiteLuminance = 65535;
    }
}
