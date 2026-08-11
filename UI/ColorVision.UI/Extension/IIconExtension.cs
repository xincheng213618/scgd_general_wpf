using ColorVision.Themes;
using System.Windows;
using System.Windows.Media;


namespace ColorVision.UI.Extension
{
    public static class IIconExtension
    {
        public static void SetIconResource(this IIcon target, string resourceName)
        {
            UpdateIcon(target, resourceName);
            SubscribeToThemeChanges(new WeakReference<IIcon>(target), resourceName);
        }

        private static void UpdateIcon(IIcon icon, string resourceKey)
        {
            if (Application.Current.TryFindResource(resourceKey) is DrawingImage drawingImage)
                icon.Icon = drawingImage;
        }

        private static void SubscribeToThemeChanges(WeakReference<IIcon> targetReference, string resourceName)
        {
            ThemeManager themeManager = ThemeManager.Current;
            ThemeChangedHandler? themeChangedHandler = null;
            themeChangedHandler = (s) =>
            {
                if (targetReference.TryGetTarget(out IIcon? icon))
                {
                    UpdateIcon(icon, resourceName);
                    return;
                }

                themeManager.CurrentUIThemeChanged -= themeChangedHandler;
            };
            themeManager.CurrentUIThemeChanged += themeChangedHandler;
        }
    }
}
