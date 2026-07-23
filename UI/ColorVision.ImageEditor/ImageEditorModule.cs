using ColorVision.UI;
using System;

namespace ColorVision.ImageEditor
{
    public static class ImageEditorModule
    {
        public const string Id = "ColorVision.ImageEditor";

        public static void Register(ModuleCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            catalog.AddBuiltIn(Id, typeof(ImageEditorModule).Assembly);
        }
    }
}
