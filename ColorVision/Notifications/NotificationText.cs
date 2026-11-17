using System.Globalization;
using System.Resources;

namespace ColorVision.Notifications;

internal static class NotificationText
{
    private static readonly ResourceManager Resources = new(
        "ColorVision.Notifications.NotificationResources", typeof(NotificationText).Assembly);

    public static string Title => Get(nameof(Title));
    public static string TotalCount => Get(nameof(TotalCount));
    public static string ClearAll => Get(nameof(ClearAll));
    public static string Empty => Get(nameof(Empty));

    private static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
