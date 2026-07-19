using ColorVision.UI;
using System;

namespace ColorVision.ImageTools
{
    public static class ImageToolsModule
    {
        public const string Id = "ColorVision.ImageTools";

        public static void Register(ModuleCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            catalog.AddBuiltIn(Id, typeof(ImageToolsModule).Assembly);
        }
    }
}
