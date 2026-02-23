using ColorVision.Themes;
using System.Windows;
using System.Windows.Media;


namespace ColorVision.UI.Extension
{
    public static class IIconExtension
    {
        public static void SetIconResource(this IIcon Class, string ResourceName)
        {
            if (Application.Current.TryFindResource(ResourceName) is DrawingImage drawingImage)
                Class.Icon = drawingImage;
            ThemeManager.Current.CurrentUIThemeChanged += (s) =>
            {
                if (Application.Current.TryFindResource(ResourceName) is DrawingImage drawingImage)
                    Class.Icon = drawingImage;
            };
        }
    }
}
