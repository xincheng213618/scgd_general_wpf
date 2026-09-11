using System.Globalization;
using System.Resources;

namespace ColorVision.Guidance;

internal static class NewUserGuideText
{
    private static readonly ResourceManager ResourceManager = new(
        "ColorVision.Guidance.NewUserGuideResources",
        typeof(NewUserGuideText).Assembly);

    internal static string Get(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    internal static string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
}
