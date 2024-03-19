using ColorVision.Services.Devices;
using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Services.Extension
{
    public static class ServicesExtension
    {
        public static void SetResource(this IIcon Class, string ResourceName ,View? view = null)
        {
            if (Application.Current.TryFindResource(ResourceName) is DrawingImage drawingImage)
                Class.Icon = drawingImage;
            if (view != null)
                view.Icon = Class.Icon;

            ThemeManager.Current.CurrentUIThemeChanged += (s) =>
            {
                if (Application.Current.TryFindResource(ResourceName) is DrawingImage drawingImage)
                    Class.Icon = drawingImage;
                if (view!=null)
                    view.Icon = Class.Icon;
            };

        }


    }
}
