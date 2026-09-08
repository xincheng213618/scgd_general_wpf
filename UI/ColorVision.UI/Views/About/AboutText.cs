using System.Globalization;
using System.Resources;

#if SPECTRUM_ABOUT
namespace Spectrum.Help.Art;
#else
namespace ColorVision.UI.Views.About;
#endif

/// <summary>Localized presentation shared by the main and Spectrum About windows.</summary>
public static class AboutText
{
    private static readonly ResourceManager Resources = new($"{typeof(AboutText).Namespace}.AboutResources", typeof(AboutText).Assembly);

    private static string Get(string name)
    {
        CultureInfo culture = CultureInfo.CurrentUICulture;
        // Preserve Chinese script/region fallback; other languages use the English presentation.
        if (culture.TwoLetterISOLanguageName != "zh") culture = CultureInfo.GetCultureInfo("en");
        return Resources.GetString(name, culture) ?? name;
    }

    public static string AboutCaption => Get(nameof(AboutCaption));
    public static string PaletteTooltip => Get(nameof(PaletteTooltip));
    public static string PaletteAutomation => Get(nameof(PaletteAutomation));
    public static string CloseTooltip => Get(nameof(CloseTooltip));
    public static string CloseAutomation => Get(nameof(CloseAutomation));
    public static string VisionDescriptor => Get(nameof(VisionDescriptor));
    public static string VisionHeadline => Get(nameof(VisionHeadline));
    public static string VisionSubline => Get(nameof(VisionSubline));
    public static string VisionCapabilities => Get(nameof(VisionCapabilities));
    public static string SpectrumTitle => Get(nameof(SpectrumTitle));
    public static string SpectrumEyebrow => Get(nameof(SpectrumEyebrow));
    public static string SpectrumHeadline => Get(nameof(SpectrumHeadline));
    public static string SpectrumSubline => Get(nameof(SpectrumSubline));
    public static string MainAboutTitle => Get(nameof(MainAboutTitle));
}
