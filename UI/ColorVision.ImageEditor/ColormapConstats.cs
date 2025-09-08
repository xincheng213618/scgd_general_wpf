#pragma warning disable CS8625
using System.Collections.Generic;

namespace ColorVision.ImageEditor
{
    public static class ColormapConstats
    {
        public static Dictionary<ColormapTypes, string> GetColormapHDictionary()
        {
            var colormapDictionary = new Dictionary<ColormapTypes, string>
        {
            { ColormapTypes.COLORMAP_AUTUMN, "Assets/Colormap/colorscale_autumn.jpg" },
            { ColormapTypes.COLORMAP_BONE, "Assets/Colormap/colorscale_bone.jpg" },
            { ColormapTypes.COLORMAP_JET, "Assets/Colormap/colorscale_jet.jpg" },
            { ColormapTypes.COLORMAP_WINTER, "Assets/Colormap/colorscale_winter.jpg" },
            { ColormapTypes.COLORMAP_RAINBOW, "Assets/Colormap/colorscale_rainbow.jpg" },
            { ColormapTypes.COLORMAP_OCEAN, "Assets/Colormap/colorscale_ocean.jpg" },
            { ColormapTypes.COLORMAP_SUMMER, "Assets/Colormap/colorscale_summer.jpg" },
            { ColormapTypes.COLORMAP_SPRING, "Assets/Colormap/colorscale_spring.jpg" },
            { ColormapTypes.COLORMAP_COOL, "Assets/Colormap/colorscale_cool.jpg" },
            { ColormapTypes.COLORMAP_HSV, "Assets/Colormap/colorscale_hsv.jpg" },
            { ColormapTypes.COLORMAP_PINK, "Assets/Colormap/colorscale_pink.jpg" },
            { ColormapTypes.COLORMAP_HOT, "Assets/Colormap/colorscale_hot.jpg" },
            { ColormapTypes.COLORMAP_PARULA, "Assets/Colormap/colorscale_parula.jpg" },
            { ColormapTypes.COLORMAP_MAGMA, "Assets/Colormap/colorscale_magma.jpg" },
            { ColormapTypes.COLORMAP_INFERNO, "Assets/Colormap/colorscale_inferno.jpg" },
            { ColormapTypes.COLORMAP_PLASMA, "Assets/Colormap/colorscale_plasma.jpg" },
            { ColormapTypes.COLORMAP_VIRIDIS, "Assets/Colormap/colorscale_viridis.jpg" },
            { ColormapTypes.COLORMAP_CIVIDIS, "Assets/Colormap/colorscale_cividis.jpg" },
            { ColormapTypes.COLORMAP_TWILIGHT, "Assets/Colormap/colorscale_twilight.jpg" },
            { ColormapTypes.COLORMAP_TWILIGHT_SHIFTED, "Assets/Colormap/colorscale_twilight_shifted.jpg" },
            { ColormapTypes.COLORMAP_TURBO, "Assets/Colormap/colorscale_turbo.jpg" },
            { ColormapTypes.COLORMAP_DEEPGREEN, "Assets/Colormap/colorscale_deepgreen.jpg" }
        };
            return colormapDictionary;
        }
    }
}
