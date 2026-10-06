using System.Globalization;

namespace ColorVision.UI.Desktop.Diagnostics;

internal static class CrashDumpText
{
    internal static string Get(string key) => Properties.Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    internal static string Format(string key, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
}
