using ColorVision.UI;
using System;

namespace ColorVision.Rbac
{
    public static class RbacModule
    {
        public const string Id = "ColorVision.Rbac";

        public static void Register(ModuleCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            catalog.AddBuiltIn(Id, typeof(RbacModule).Assembly);
        }
    }
}
