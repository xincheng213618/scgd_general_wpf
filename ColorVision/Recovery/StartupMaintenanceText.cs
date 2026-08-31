using System.Globalization;
using System.Resources;

namespace ColorVision.Recovery;

internal static class StartupMaintenanceText
{
    private static readonly ResourceManager Resources = new("ColorVision.Recovery.StartupMaintenanceResources", typeof(StartupMaintenanceText).Assembly);

    internal static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
