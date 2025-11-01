using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Views;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Engine.Extension
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

        public static void SetIconResource(this IIcon Class, string ResourceName, View? view = null)
        {
            if (Application.Current.TryFindResource(ResourceName) is DrawingImage drawingImage)
                Class.Icon = drawingImage;
            if (view != null)
                view.Icon = Class.Icon;

            ThemeManager.Current.CurrentUIThemeChanged += (s) =>
            {
                if (Application.Current.TryFindResource(ResourceName) is DrawingImage drawingImage)
                    Class.Icon = drawingImage;
                if (view != null)
                    view.Icon = Class.Icon;
            };
        }
    }
}
