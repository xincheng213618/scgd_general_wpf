using ColorVision.UI;
using System;

namespace ColorVision.UI.Desktop
{
    public static class DesktopModule
    {
        public const string Id = "ColorVision.UI.Desktop";

        public static void Register(ModuleCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            catalog.AddBuiltIn(Id, typeof(DesktopModule).Assembly);
        }
    }
}
