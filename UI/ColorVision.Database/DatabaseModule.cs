using ColorVision.UI;
using System;

namespace ColorVision.Database
{
    public static class DatabaseModule
    {
        public const string Id = "ColorVision.Database";

        public static void Register(ModuleCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            catalog.AddBuiltIn(Id, typeof(DatabaseModule).Assembly);
        }
    }
}
